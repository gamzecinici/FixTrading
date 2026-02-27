using System.Collections.Concurrent;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.Infrastructure.MongoDb;

/// <summary>
/// 1 dakika boyunca gelen TÜM market data kayıtlarını biriktirir,
/// flush zamanı gelince hepsini InsertMany ile MongoDB'ye toplu yazar.
/// </summary>
public class MongoMarketDataBuffer : IMarketDataBuffer, IDisposable
{
    private readonly IMongoCollection<DtoMarketData> _collection;

    // 1 dakika boyunca gelen TÜM verileri biriktiren thread-safe liste
    private readonly ConcurrentBag<DtoMarketData> _buffer = new();

    private readonly Timer _flushTimer;
    private readonly int _flushIntervalMs;
    private bool _disposed;

    // DI container bu constructor'ı otomatik çağırır (new ile çağrılmaz)
    public MongoMarketDataBuffer(MongoClient mongoClient, IOptions<MongoMarketDataOptions> options)
    {
        var opts = options.Value;
        var database = mongoClient.GetDatabase(opts.DatabaseName);
        _collection = database.GetCollection<DtoMarketData>(opts.CollectionName);
        _flushIntervalMs = opts.FlushIntervalSeconds * 1000;

        _flushTimer = new Timer(FlushBuffer, null, _flushIntervalMs, _flushIntervalMs);
        Console.WriteLine($"[MongoMarketData] Buffer başlatıldı, her {opts.FlushIntervalSeconds} sn. tüm veriler toplu yazılacak.");
    }

    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public void Add(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;
        symbol = symbol.Trim().ToUpper().Replace("/", "");

        var utcNow = DateTime.UtcNow;
        var turkeyTime = utcNow + TurkeyOffset;

        var dto = new DtoMarketData
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = (bid + ask) / 2,
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd.MM.yyyy HH:mm")
        };

        _buffer.Add(dto);
    }

    // 60 sn dolunca buffer'daki TÜM verileri alıp MongoDB'ye toplu yazar
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
            Console.WriteLine($"[MongoMarketData] {snapshot.Count} kayıt yazıldı.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MongoMarketDataBuffer] Bulk insert hata: {ex.Message}");
            foreach (var item in snapshot)
                _buffer.Add(item);
        }
    }

    // Uygulama kapanırken DI tarafından otomatik çağrılır
    public void Dispose()
    {
        if (_disposed) return;
        _flushTimer.Dispose();
        FlushBuffer(null);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// appsettings'ten okunan Mongo ayarları.
/// </summary>
public class MongoMarketDataOptions
{
    public const string SectionName = "MongoMarketData";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "FixTrading";
    public string CollectionName { get; set; } = "marketData";
    public int FlushIntervalSeconds { get; set; } = 60;
}
