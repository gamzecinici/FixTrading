using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace FixTrading.Infrastructure.MongoDb;

// Bu sınıf, IMongoLatestPriceReader arayüzünü uygulayarak MongoDB'den en son fiyat bilgisini okumak için kullanılır.
//Redis'te bulunmayan verileri almak için tasarlanmıştır. MongoDB bağlantısı ve ayarları constructor'da alınır
public class MongoLatestPriceReader : IMongoLatestPriceReader
{
    // Dokümanlardaki Mongo _id ve sınıfta tanımsız alanlar okunurken FormatException vermesin diye.
    static MongoLatestPriceReader()
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


    // Constructor, MongoDB bağlantısı ve ayarları alır
    public MongoLatestPriceReader(MongoClient mongoClient, IOptions<MongoMarketDataOptions> options)
    {
        var opts = options.Value;   
        var database = mongoClient.GetDatabase(opts.DatabaseName);   
        _collection = database.GetCollection<DtoMarketData>(opts.CollectionName);
    }

    // Bu metot, belirtilen sembol için en son fiyat bilgisini asenkron olarak getirir. Eğer sembol bulunamazsa null döner.
    public async Task<DtoMarketData?> GetLatestAsync(string symbol)
    {
        symbol = symbol.Trim().ToUpper().Replace("/", "");
        var cursor = await _collection
            .Find(x => x.Symbol == symbol)
            .SortByDescending(x => x.Timestamp)
            .Limit(1)
            .FirstOrDefaultAsync();
        return cursor;
    }


    // Bu metot, tüm semboller için en son fiyat bilgilerini asenkron olarak getirir. Sonuçlar sembol sırasına göre sıralanır.
    public async Task<List<DtoMarketData>> GetAllLatestAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$sort", new BsonDocument("Timestamp", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Symbol" },
                { "doc", new BsonDocument("$first", "$$ROOT") }
            }),
            new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$doc"))
        };

        var results = await _collection.Aggregate<DtoMarketData>(pipeline).ToListAsync();
        return results.OrderBy(x => x.Symbol).ToList();
    }
}
