using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using FixTrading.Application.Interfaces;
using FixTrading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;


// Sistem parametrelerini yönetmek için servis sınıfı
namespace FixTrading.Infrastructure.Services;

// Bu sınıf, ISystemParameterService arayüzünü uygular ve sistem parametrelerini veritabanindan okuyup gunceller.
public class SystemParameterService : ISystemParameterService
{
    private readonly IServiceScopeFactory _scopeFactory;                  //Her islem icin yeni bir servis kapsami olusturmak icin kullanilir.
    private readonly ILogger<SystemParameterService> _logger;             // Loglama islemleri için kullanilir.
    private readonly IConnectionMultiplexer _redis;                       // Redis baglantisi

    private const string RedisCacheKeyPrefix = "cache:sysparam:";
    private const string RedisAllFilesKey = "cache:sysparam:all_files";
    private static readonly TimeSpan RedisTtl = TimeSpan.FromHours(2);    //2saatte bir Redis cache'inin guncellenmesi saglanir.

    // Local Cache mekanizmasi: Dosya adlarina gore JSON verilerini ve yuklenme zamanlarini saklar.
    // Bu sayede sik erisilen parametreler veritabanina veya Redis'e tekrar tekrar gitmeden hizlica alinabilir.
    private readonly ConcurrentDictionary<string, (string Json, DateTime LoadedAt)> _localCache = new();  
    private static readonly TimeSpan LocalCacheDuration = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, bool> _missingLogged = new();


    //bu servis, veritabani islemleri icin yeni bir servis kapsami olusturmak ve loglama yapmak icin gerekli bagimliliklari alir
    public SystemParameterService(IServiceScopeFactory scopeFactory, ILogger<SystemParameterService> logger, IConnectionMultiplexer redis)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _redis = redis;
    }


    // Tüm parametreleri Redis-first mantigiyla getirir. Öncelikle Redis'ten cekmeye calisir, eger yoksa veritabanindan ceker ve Redis'e kaydeder.
    public async Task<List<object>> GetAllParametersAsync()
    {
        var db = _redis.GetDatabase();                                                                 //Redis uzerinde islem yapmak icin database nesnesi alinir.

        // Once Redis'te cache'lenmis tum parametreler cekilmeye calisir. Eger Redis'te bu veriler varsa, bu veriler kullanilir
        var cached = await db.StringGetAsync(RedisAllFilesKey);
        if (cached.HasValue)
        {
            try 
            {
                // JSON'dan doğrudan hedef tipe (SystemParameterEntity) deserialize et
                var list = JsonSerializer.Deserialize<List<FixTrading.Persistence.Entities.SystemParameterEntity>>(cached.ToString());
                return list?.Cast<object>().ToList() ?? new List<object>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cached system parameter list could not be deserialized.");
            }
        }

        // Redis'te cache'lenmis veriler bulunamazsa, veritabanindan tum parametreler cekilir. Bu islemi yaparken, performans icin AsNoTracking kullanilir ve sadece gerekli alanlar secilir.
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();                   
        var parameters = await dbContext.SystemParameters
            .AsNoTracking()
            .OrderBy(x => x.DosyaAdi)
            .ToListAsync();

        // Eger veritabanindan parametreler cekildiyse, bu parametreler Redis'e cache'lenir. Bu sayede sonraki taleplerde Redis'ten hizlica cekilebilirler.
        if (parameters.Any())
        {
            await db.StringSetAsync(RedisAllFilesKey, JsonSerializer.Serialize(parameters), RedisTtl);
        }

        return parameters.Cast<object>().ToList();                                //Son olarak elde edilen parametre listesi (DB'den veya Redis'ten) geri dondurulur 
    }
    
    // Belirli bir dosya adi icin sistem parametrelerini getirir. Eger parametre bulunamazsa null dener.
    public async Task<Dictionary<string, string>?> GetConfigAsync(string fileName)   
    {
        //İlk olarak local cache kontrol edilir. Eğer cache'te geçerli bir veri varsa, bu veri kullanılır. Aksi halde Redis cache kontrol edilir.
        try
        {
            if (_localCache.TryGetValue(fileName, out var localCached) && DateTime.UtcNow - localCached.LoadedAt < LocalCacheDuration)
                return JsonToDictionary(localCached.Json);

            var redisKey = $"{RedisCacheKeyPrefix}{fileName}";                              //Rediste kullanilacak anahtar olusturulur.
            var db = _redis.GetDatabase();                                                  //Redis db nesnesi alinir
            var redisCached = await db.StringGetAsync(redisKey);                            //Redis kontrol edilir ve eger cache'lenmis bir deger varsa alinir.

            // Rediste bu dosya adi icin cache'lenmis bir deger var mi kontrol edilir. Eger varsa, bu deger kullanilir ve local cache'e de eklenir.
            if (redisCached.HasValue)
            {
                var json = redisCached.ToString();
                _localCache[fileName] = (json, DateTime.UtcNow);
                return JsonToDictionary(json);                                          // Redis'ten gelen JSON verisi Dictionary formatina donusturulur ve dondurulur.
            }


            using var scope = _scopeFactory.CreateScope();                             // Veritabanina erisim icin yeni bir servis kapsamı olusturulur.
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();


            // Veritabaninda bu dosya adi icin bir parametre aranir. Eger bulunamazsa, loglama yapilir ve null dondurulur.
            var param = await dbContext.SystemParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DosyaAdi == fileName);


            if (param == null)
            {
                if (_missingLogged.TryAdd(fileName, true))
                    _logger.LogWarning("System parameter not found for file: {FileName} (will not log again until restart)", fileName);
                return null;
            }

            //Veri bulunduktan sonra, bu veri Redis'e cache'lenir ve local cache'e de eklenir. Son olarak, JSON verisi Dictionary formatina donusturulur ve dondurulur.
            await db.StringSetAsync(redisKey, param.Config, RedisTtl);
            _localCache[fileName] = (param.Config, DateTime.UtcNow);
            _missingLogged.TryRemove(fileName, out _);
            return JsonToDictionary(param.Config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading system parameter for file: {FileName}", fileName);
            return null;
        }
    }


    // Belirli bir dosya adi icin sistem parametrelerini gunceller. Guncelleme islemi basarili olursa true, aksi halde false doner.
    public async Task<bool> UpdateConfigAsync(string fileName, Dictionary<string, string> config, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
        {
            _logger.LogWarning("Update attempt for {FileName} rejected: updatedBy is empty.", fileName);
            return false;
        }

        if (config == null) return false;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Veritabaninda bu dosya adi icin bir parametre aranir. Eger bulunamazsa, loglama yapilir ve false dondurulur.
            var param = await dbContext.SystemParameters
                .FirstOrDefaultAsync(x => x.DosyaAdi == fileName);

            if (param == null)
            {
                _logger.LogWarning("System parameter not found for update: {FileName}", fileName);
                return false;
            }

            EnsureKeySetUnchanged(param.Config, config.Keys);

            // Parametre bulunursa, bu parametrenin Config alani guncellenir ve guncelleyen kullanici ile guncellenme tarihi de guncellenir.
            param.Config = DictionaryToJson(config);
            param.GuncelleyenKullanici = updatedBy;
            param.GuncellenmeTarihi = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            // Guncelleme islemi basarili olduktan sonra, ilgili Redis cache'leri silinir ve local cache'ten de kaldirilir.
            var db = _redis.GetDatabase();
            var redisKey = $"{RedisCacheKeyPrefix}{fileName}";
            await db.KeyDeleteAsync(new RedisKey[] { redisKey, RedisAllFilesKey });
            _localCache.TryRemove(fileName, out _);

            _logger.LogInformation("System parameter {FileName} updated by {User}", fileName, updatedBy);
            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system parameter for file: {FileName} by {User}", fileName, updatedBy);
            return false;
        }
    }


    // Birden fazla dosya adi icin sistem parametrelerini toplu olarak gunceller veya olusturur.
    // Guncelleme islemi basarili olursa true, aksi halde false dener.
    public async Task<bool> BatchUpdateConfigsAsync(Dictionary<string, string> updates, string updatedBy)
    {

        // Guncelleme islemi yapacak kullanicinin bilgisi bos veya sadece beyaz karakterlerden olusuyorsa, bu guncelleme islemi reddedilir ve false dondurulur.
        if (string.IsNullOrWhiteSpace(updatedBy)) return false;
       
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(RedisAllFilesKey);                                        //Redis'teki tum dosya parametre listesi silinir

        if (updates == null || updates.Count == 0) return true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Veritabanindan, guncellenecek dosya adlarina sahip parametreler cekilir. Bu sayede sadece guncellenecek parametreler uzerinde islem yapilir.
            var fileNames = updates.Keys.ToList();
            var parameters = await dbContext.SystemParameters
                .Where(x => fileNames.Contains(x.DosyaAdi))
                .ToListAsync();

            // Her bir parametre icin, eger guncellenecek bir deger varsa, bu deger parametreye atanir ve guncelleme bilgileri (kullanici ve tarih) guncellenir.
            foreach (var param in parameters)
            {
                if (updates.TryGetValue(param.DosyaAdi, out var newConfig))
                {
                    EnsureKeySetUnchanged(param.Config, ExtractJsonKeys(newConfig));
                    param.Config = newConfig;
                    param.GuncelleyenKullanici = updatedBy;
                    param.GuncellenmeTarihi = DateTime.UtcNow;

                    
                    var redisKey = $"{RedisCacheKeyPrefix}{param.DosyaAdi}";
                    await db.KeyDeleteAsync(redisKey);
                    _localCache.TryRemove(param.DosyaAdi, out _);         // Her guncellenen parametre icin, ilgili Redis cache'i silinir ve local cache'ten de kaldirilir. Bu sayede sonraki taleplerde guncellenmis veriler Redis'ten veya veritabanindan alinabilir.
                }
            }

            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BatchUpdateConfigsAsync by {User}", updatedBy);
            return false;
        }
    }


    // JSON formatindaki bir string'i Dictionary<string, string> formatina donusturur. Eger donusum basarisiz olursa null dener.
    private static Dictionary<string, string> JsonToDictionary(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText();
                dict[prop.Name] = value;
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }


    // Dictionary<string, string> formatındaki bir veriyi JSON formatına donustururr.
    // Bu yontem, degerlerin turune gore uygun JSON turunu yazmaya calisir (null, boolean, number, string).
    private static string DictionaryToJson(Dictionary<string, string> config)
    {
        var options = new JsonWriterOptions 
        { 
            Indented = false, 
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, options);
        writer.WriteStartObject();
        foreach (var kvp in config)
        {
            writer.WritePropertyName(kvp.Key);
            if (kvp.Value == "null")
                writer.WriteNullValue();
            else if (kvp.Value == "true")
                writer.WriteBooleanValue(true);
            else if (kvp.Value == "false")
                writer.WriteBooleanValue(false);
            else if (long.TryParse(kvp.Value, out var longVal))
                writer.WriteNumberValue(longVal);
            else if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                writer.WriteNumberValue(doubleVal);
            else
                writer.WriteStringValue(kvp.Value);
        }
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void EnsureKeySetUnchanged(string existingConfigJson, IEnumerable<string> incomingKeys)
    {
        var existingKeySet = ExtractJsonKeys(existingConfigJson);
        var incomingKeySet = new HashSet<string>(incomingKeys, StringComparer.Ordinal);

        if (!existingKeySet.SetEquals(incomingKeySet))
        {
            throw new InvalidOperationException("Parametre anahtarları değiştirilemez.");
        }
    }

    private static HashSet<string> ExtractJsonKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
