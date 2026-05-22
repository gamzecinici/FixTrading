using System.Globalization;
using FixTrading.Common.Dtos.MarketData;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FixTrading.API.Services;

/// <summary>
/// Fiyat geçmişi aralıklarını UI beklentisiyle hizalar:
/// - 1d: bugün (Europe/Istanbul) 00:00'dan itibaren
/// - 1w: son 7 gün (bugün dahil), Istanbul yerel gün sınırlarıyla
/// - 1m: bu ayın 1'i 00:00'dan itibaren
/// - 1y: son 12 ay (bu ay dahil), ay başlarından itibaren
/// - all: üst sınır yok (pratikte son 10 yıl)
/// </summary>
public static class PriceHistoryQuery
{
    private static readonly TimeZoneInfo TurkeyTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

    public static (DateTime UtcStartInclusive, DateTime? UtcEndExclusive) GetUtcBounds(string? range)
    {
        var r = (range ?? "1d").Trim().ToLowerInvariant();
        var nowUtc = DateTime.UtcNow;

        if (r == "all")
        {
            // Çok büyük aralıklar performansı bozmasın diye makul bir alt sınır.
            return (nowUtc.AddYears(-10), null);
        }

        var nowTr = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TurkeyTz);

        if (r == "1d")
        {
            var startTr = new DateTime(nowTr.Year, nowTr.Month, nowTr.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTr, TurkeyTz);
            return (startUtc, null);
        }

        if (r == "1w")
        {
            // Bugün dahil 7 gün: bugünün başı - 6 gün
            var todayStartTr = new DateTime(nowTr.Year, nowTr.Month, nowTr.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var startTr = todayStartTr.AddDays(-6);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTr, TurkeyTz);
            return (startUtc, null);
        }

        if (r == "1m")
        {
            var startTr = new DateTime(nowTr.Year, nowTr.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTr, TurkeyTz);
            return (startUtc, null);
        }

        if (r == "1y")
        {
            // Bu ayın başından geriye 11 ay daha (toplam 12 ay penceresi)
            var monthStartTr = new DateTime(nowTr.Year, nowTr.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var startTr = monthStartTr.AddMonths(-11);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startTr, TurkeyTz);
            return (startUtc, null);
        }

        // Bilinmeyen aralıklar: güvenli varsayılan bugün
        var fallbackTr = new DateTime(nowTr.Year, nowTr.Month, nowTr.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var fallbackUtc = TimeZoneInfo.ConvertTimeToUtc(fallbackTr, TurkeyTz);
        return (fallbackUtc, null);
    }

    public static string FormatUtcForClient(DateTime utc)
    {
        // DateTimeKind.Utc olmasa bile Mongo'dan gelen değerler genelde UTC kabul edilir.
        var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return u.ToString("o", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Ham tick yerine TR takvimine göre kovalara ($dateTrunc) gruplayarak döner.
    /// Böylece 1y gibi uzun aralıklarda Find().Limit(100000) son tick'leri kesip tek ay gösterme hatası oluşmaz.
    /// </summary>
    public static async Task<List<PriceHistoryAggregateRow>> LoadAggregatedHistoryAsync(
        IMongoCollection<DtoMarketData> collection,
        string symbol,
        string? range,
        CancellationToken ct = default)
    {
        var (startUtc, endUtcExclusive) = GetUtcBounds(range);
        var r = (range ?? "1d").Trim().ToLowerInvariant();
        var unit = r switch
        {
            "1d" => "hour",
            "1w" or "1m" => "day",
            _ => "month" // 1y, all, bilinmeyen
        };

        var tsFilter = new BsonDocument("$gte", startUtc);
        if (endUtcExclusive is not null)
            tsFilter.Add("$lt", endUtcExclusive.Value);

        var match = new BsonDocument("$match", new BsonDocument
        {
            { "Symbol", symbol },
            { "Timestamp", tsFilter }
        });

        var group = new BsonDocument("$group", new BsonDocument
        {
            {
                "_id",
                new BsonDocument("$dateTrunc", new BsonDocument
                {
                    { "date", "$Timestamp" },
                    { "unit", unit },
                    { "timezone", "Europe/Istanbul" }
                })
            },
            { "Bid", new BsonDocument("$avg", "$Bid") },
            { "Ask", new BsonDocument("$avg", "$Ask") },
            { "Mid", new BsonDocument("$avg", "$Mid") },
            { "Spread", new BsonDocument("$avg", "$Spread") }
        });

        var sort = new BsonDocument("$sort", new BsonDocument("_id", -1));
        BsonDocument[] pipeline = [match, group, sort];

        var docs = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);

        var list = new List<PriceHistoryAggregateRow>(docs.Count);
        foreach (var doc in docs)
        {
            if (!doc.TryGetValue("_id", out var idVal) || !idVal.IsValidDateTime)
                continue;

            var bucketUtc = DateTime.SpecifyKind(idVal.ToUniversalTime(), DateTimeKind.Utc);
            list.Add(new PriceHistoryAggregateRow(
                bucketUtc,
                ReadMoney(doc, "Bid"),
                ReadMoney(doc, "Ask"),
                ReadMoney(doc, "Mid"),
                ReadMoney(doc, "Spread")));
        }

        return list;
    }

    private static decimal ReadMoney(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var v) || v.IsBsonNull)
            return 0m;
        if (v.IsDecimal128)
            return ((BsonDecimal128)v).ToDecimal();
        if (v.IsDouble)
            return (decimal)v.AsDouble;
        if (v.IsInt32)
            return v.AsInt32;
        if (v.IsInt64)
            return v.AsInt64;
        return 0m;
    }
}

/// <summary>Mongo $dateTrunc ile üretilen tek kova (bucket) satırı.</summary>
public sealed record PriceHistoryAggregateRow(
    DateTime BucketUtc,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    decimal Spread);
