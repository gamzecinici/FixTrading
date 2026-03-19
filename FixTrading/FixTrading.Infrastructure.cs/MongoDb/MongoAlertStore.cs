using FixTrading.Common.Dtos.Alert;
using FixTrading.Domain.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace FixTrading.Infrastructure.MongoDb;

//
public class MongoAlertStore : IAlertStore
{
    private readonly IMongoCollection<DtoAlert> _collection;

    public const string AlertsCollectionName = "alerts";

    static MongoAlertStore()
    {
        // AutoMap tüm public property'leri eşler; SetIgnoreExtraElements ise _id gibi fazladan alanları okurken hata vermez.
        if (!BsonClassMap.IsClassMapRegistered(typeof(DtoAlert)))
            BsonClassMap.RegisterClassMap<DtoAlert>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
    }

    public MongoAlertStore(MongoClient mongoClient, IOptions<MongoMarketDataOptions> options)
    {
        var opts = options.Value;
        var database = mongoClient.GetDatabase(opts.DatabaseName);
        _collection = database.GetCollection<DtoAlert>(AlertsCollectionName);
    }

    public async Task WriteAsync(DtoAlert alert)
    {
        await _collection.InsertOneAsync(alert);
    }
}
