using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FixTrading.Application.Interfaces;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using FixTrading.Common.Dtos.Options;
using FixTrading.Common.Pricing;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace FixTrading.Infrastructure.MongoDb;

// MongoDB'ye market data yazmak icin kullanilan buffer sinifi. 1 dakika boyunca gelen tum verileri biriktirir ve sonra toplu olarak MongoDB'ye yazar. 
public sealed class MongoMarketDataBuffer : IMarketDataBuffer, IDisposable
{  
    // Insert/upsert tarafinda da driver bazen dokumana _id ekler; round-trip ve okumada ekstra alan toleransi.
    static MongoMarketDataBuffer()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoMarketData)))
        {
            BsonClassMap.RegisterClassMap<DtoMarketData>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    private readonly IMongoCollection<DtoMarketData> _collection;
    private readonly ISystemParameterService _systemParameterService;

    // 1 dakika boyunca gelen TUM verileri biriktiren thread-safe liste
    private readonly ConcurrentBag<DtoMarketData> _buffer = new();

    private readonly Timer _flushTimer;
    private readonly int _flushIntervalMs;
    private bool _disposed;

    // DI container bu constructor'i otomatik cagirir (new ile cagrilmaz). fixapp icinde IMarketDataBuffer fonksiyonu
    public MongoMarketDataBuffer(MongoClient mongoClient, IOptions<MongoMarketDataOptions> options, ISystemParameterService systemParameterService)
    {
        _systemParameterService = systemParameterService;
        var opts = options.Value;   //config degerlerini alir

        try
        {

            // MongoSettings parametrelerini sistem parametrelerinden dinamik olarak alalim.
            var config = _systemParameterService.GetConfigAsync("MongoSettings").GetAwaiter().GetResult();
            if (config != null)
            {
                if (config.TryGetValue("DatabaseName", out var db) && db != null) opts.DatabaseName = db;
                if (config.TryGetValue("CollectionName", out var coll) && coll != null) opts.CollectionName = coll;
                if (config.TryGetValue("FlushIntervalSeconds", out var flushStr) && int.TryParse(flushStr, out var flush)) opts.FlushIntervalSeconds = flush;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MongoMarketData] Dinamik MongoSettings okunamadı: {ex.Message}");
        }

        var database = mongoClient.GetDatabase(opts.DatabaseName);
        _collection = database.GetCollection<DtoMarketData>(opts.CollectionName);
        _flushIntervalMs = opts.FlushIntervalSeconds * 1000;

        _flushTimer = new Timer(FlushBuffer, null, _flushIntervalMs, _flushIntervalMs);
        Console.WriteLine($"[MongoMarketData] Buffer baslatildi, her {opts.FlushIntervalSeconds} sn. tum veriler toplu yazilacak.");
    }

    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);


    // Buffer'a yeni market data ekler. 1 dakika boyunca gelen tum veriler bu buffer'da birikir, sonra toplu olarak MongoDB'ye yazilir.
    public void Add(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var u = DateTime.UtcNow;
        var utcNow = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, u.Second, DateTimeKind.Utc);
        var turkeyTime = utcNow + TurkeyOffset;

        var (mid, spread) = PricingCalculator.FromBidAsk(bid, ask);
        var dto = new DtoMarketData
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = mid,
            Spread = spread,
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
        };

        _buffer.Add(dto);
    }

    // Buffer'daki verileri hemen kalici depoya yazar. FIX disconnect sirasinda son verilerin kaybolmamasi icin cagirilir.
    public void Flush()
    {
        FlushBuffer(null);
    }

    // 60 sn dolunca buffer'daki tum  verileri alip MongoDB'ye toplu yazar
    private void FlushBuffer(object? _)
    {
        if (_buffer.IsEmpty) return;

        var snapshot = new List<DtoMarketData>();
        while (_buffer.TryTake(out var dto))
            snapshot.Add(dto);

        if (snapshot.Count == 0) return;

        try
        {
            _collection.InsertMany(snapshot, new InsertManyOptions { IsOrdered = false });
            
            var offsetHours = 3;
            try
            {
                // MongoSettings icinde TurkeyOffsetHours parametresi varsa onu kullan, yoksa default 3 saat kalir
                var config = _systemParameterService.GetConfigAsync("MongoSettings").GetAwaiter().GetResult();
                if (config != null && config.TryGetValue("TurkeyOffsetHours", out var offsetStr) && int.TryParse(offsetStr, out var offset))
                    offsetHours = offset;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoMarketData] TurkeyOffsetHours okunamadı: {ex.Message}");
            }

            var time = DateTime.UtcNow.Add(TimeSpan.FromHours(offsetHours)).ToString("HH:mm:ss");
            Console.WriteLine($"[MongoMarketData] {snapshot.Count} kayit MongoDB'ye toplu yazildi. ({time})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MongoMarketDataBuffer] Bulk insert hata: {ex.Message}");
            foreach (var item in snapshot)
                _buffer.Add(item);
        }
    }

    // Uygulama kapanirken DI tarafindan otomatik cagirilir
    public void Dispose()
    {
        if (_disposed) return;
        _flushTimer.Dispose();
        FlushBuffer(null);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
