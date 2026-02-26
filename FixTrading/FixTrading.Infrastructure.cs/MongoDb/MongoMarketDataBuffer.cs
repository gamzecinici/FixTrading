using System.Collections.Concurrent;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FixTrading.Infrastructure.MongoDb;

/// <summary>
/// Market data buffer. Verileri bellekte tutar, periyodik olarak MongoDB'ye bulk insert yapar.
/// MongoClient Singleton ile yönetilir; performans için InsertMany ordered=false kullanır.
/// </summary>
public class MongoMarketDataBuffer : IMarketDataBuffer, IDisposable
{
    private readonly IMongoCollection<DtoMarketData> _collection;
    private readonly ConcurrentDictionary<string, DtoMarketData> _latestBySymbol = new();
    private readonly Timer _flushTimer;
    private readonly int _flushIntervalMs;
    private bool _disposed;

    public MongoMarketDataBuffer(MongoClient mongoClient, IOptions<MongoMarketDataOptions> options)
    {
        var opts = options.Value;
        var database = mongoClient.GetDatabase(opts.DatabaseName);
        _collection = database.GetCollection<DtoMarketData>(opts.CollectionName);
        _flushIntervalMs = opts.FlushIntervalSeconds * 1000;

        _flushTimer = new Timer(FlushBuffer, null, _flushIntervalMs, _flushIntervalMs);
        Console.WriteLine($"[MongoMarketData] Buffer başlatıldı, her {opts.FlushIntervalSeconds} sn. sembol başına 1 kayıt yazılacak.");
    }

    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public void Add(string symbol, decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0) return;
        symbol = symbol.Trim().ToUpper().Replace("/", ""); // EUR/USD = EURUSD

        var utcNow = DateTime.UtcNow;
        var turkeyTime = utcNow + TurkeyOffset;

        var dto = new DtoMarketData
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Mid = (bid + ask) / 2,
            Timestamp = utcNow,
            TimestampFormatted = turkeyTime.ToString("dd/MM/yyyy HH:mm")
        };
        _latestBySymbol.AddOrUpdate(symbol, dto, (_, _) => dto);
    }

    private void FlushBuffer(object? _)
    {
        if (_latestBySymbol.IsEmpty) return;

        var keys = _latestBySymbol.Keys.ToList();
        var snapshot = new List<DtoMarketData>();
        foreach (var symbol in keys)
        {
            if (_latestBySymbol.TryRemove(symbol, out var dto))
                snapshot.Add(dto);
        }

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
                _latestBySymbol.TryAdd(item.Symbol, item);
        }
    }

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
/// MongoDB market data buffer ayarları.
/// </summary>
public class MongoMarketDataOptions
{
    public const string SectionName = "MongoMarketData";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "FixTrading";
    public string CollectionName { get; set; } = "marketData";
    public int FlushIntervalSeconds { get; set; } = 60;
}
