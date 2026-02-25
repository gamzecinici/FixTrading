# FixTrading Projesi - Teknik Mimari Rapor

Bu rapor, projenin genel kod mantığını, katmanlar arası veri akışını ve mimari kararları açıklar.

---

## 1. Projenin Genel Mimari Yapısı

### Katman Akışı: API → Application → Infrastructure → Persistence

Proje katmanlı (layered) mimari kullanır. Veri ve çağrılar tek yönlü akar:

```
┌─────────────────────────────────────────────────────────────────┐
│                         API (Giriş Noktası)                       │
│  TestController, OrderHandler, FixListenerWorker, Program.cs     │
└────────────────────────────┬────────────────────────────────────┘
                             │ çağrı yapar
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Application (İş Katmanı)                       │
│  IOrderService → OrderService                                     │
└────────────────────────────┬────────────────────────────────────┘
                             │ kullanır
          ┌──────────────────┼──────────────────┐
          │                  │                  │
          ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────────┐  ┌──────────────────────────┐
│   Domain     │  │  Infrastructure  │  │    Persistence (DB)       │
│ IBaseRepository│ │ IFixSession,    │  │ OrderRepository, AppDbContext│
│ (Interface)  │  │ QuickFixSession  │  │ DtoOrder (EF entity)      │
└──────────────┘  └──────────────────┘  └──────────────────────────┘
```

### Katman Referansları

| Katman | Bağımlı Olduğu | Açıklama |
|--------|----------------|----------|
| **API** | Application, Infrastructure, Persistence | Giriş katmanı; HTTP, background service |
| **Application** | Domain, Common | İş kuralları; Persistence’a doğrudan referans vermez |
| **Infrastructure** | Application | FIX protokolü, harici servisler |
| **Persistence** | Domain, Common | Veritabanı erişimi |
| **Domain** | - | Sadece interface’ler; hiçbir katmana bağımlı değil |

### Neden Bu Akış?

- **API**: HTTP isteklerini alır, Application veya Infrastructure servislerine yönlendirir.
- **Application**: İş kurallarını uygular; veriye erişmek için sadece interface kullanır.
- **Infrastructure**: FIX protokolü gibi dış sistemlere erişim.
- **Persistence**: EF Core ve veritabanı işlemleri sadece burada yapılır.

---

## 2. Dependency Injection (DI) Yapısı

### Merkezi Kayıt: Program.cs

Tüm servis kayıtları `ConfigureServices` içinde yapılır:

```csharp
// Program.cs, satır 35-52
static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();

    services.AddApplication();        // Application katmanı
    services.AddInfrastructure();     // Infrastructure katmanı
    services.AddPersistence(...);     // Persistence katmanı

    services.AddScoped<OrderHandler>();
    services.AddHostedService<FixListenerWorker>();
}
```

### Katman Bazlı DI Kayıtları

**Application katmanı** (`AddApplication`):
```csharp
services.AddScoped<IOrderService, OrderService>();
```
- `IOrderService` istenen yerde `OrderService` instance’ı alır.
- Her HTTP isteğinde yeni bir `OrderService` oluşur (Scoped).

**Persistence katmanı** (`AddPersistence`):
```csharp
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
services.AddScoped<IBaseRepository<DtoOrder>, OrderRepository>();
```
- `DbContext` ve `OrderRepository` Scoped; istek boyunca aynı instance kullanılır.

**Infrastructure katmanı** (`AddInfrastructure`):
```csharp
services.AddSingleton<FixApp>();
services.AddSingleton<IFixSession, QuickFixSession>();
```
- FIX bağlantısı uygulama genelinde tek instance (Singleton).

### Yaşam Döngüsü Özeti

| Servis | Lifetime | Sebep |
|--------|----------|-------|
| OrderService, OrderRepository, DbContext | Scoped | İstek başına izole veri erişimi |
| OrderHandler | Scoped | İstek içi kullanım |
| FixApp, IFixSession | Singleton | Tek FIX bağlantısı |
| FixListenerWorker | Singleton (Hosted) | Uygulama boyunca arka plan servisi |

### Çözümleme Zinciri Örneği

Bir HTTP isteği `GET /api/Test/list` geldiğinde:

1. ASP.NET Core `TestController` instance’ı oluşturur.
2. DI, constructor’da `IOrderService` için `OrderService` verir.
3. `OrderService` için `IBaseRepository<DtoOrder>` → `OrderRepository` verilir.
4. `OrderRepository` için `AppDbContext` verilir.
5. Tüm zincir aynı Scoped scope içinde çözülür.

---

## 3. EF Core Kullanımı

### Kullanıldığı Yerler

EF Core **sadece Persistence katmanında** kullanılır.

| Dosya | Rol |
|-------|-----|
| `AppDbContext.cs` | DbContext tanımı |
| `DtoOrder.cs` | FixSymbol tablosu eşlemesi (hem DTO hem entity) |
| `OrderRepository.cs` | Veritabanı sorguları |

### AppDbContext

```csharp
// Persistence/AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<DtoOrder> FixSymbols { get; set; }  // FixSymbol tablosu
    public DbSet<InstrumentEntity> Instruments { get; set; }
    public DbSet<TradeEntity> Trades { get; set; }
}
```

- `DtoOrder` doğrudan EF entity olarak kullanılır; ayrı entity sınıfı yoktur.
- Connection string `AddPersistence` ile `UseNpgsql` üzerinden verilir.

### OrderRepository İçindeki Kullanım

```csharp
// Örnek: Tüm kayıtları getir
return await _context.MarketData
    .AsNoTracking()           // Read-only, performans için
    .ToListAsync();
```

- EF sorguları `DtoOrder` üzerinde çalışır; ayrı mapping gerekmez.

### Diğer Katmanlarda EF Yok

- **Application**: `IBaseRepository<DtoOrder>` kullanır; EF tipleri yok.
- **API**: `IOrderService` veya `OrderHandler` kullanır; EF bilgisi yok.
- **Domain**: Sadece interface; hiç EF referansı yok.

---

## 4. Interface Kullanımı ve Avantajları

### Kullanılan Interface’ler

| Interface | Konum | Implementasyon | Amaç |
|-----------|-------|----------------|------|
| `IOrderService` | Application | OrderService | İş mantığı soyutlaması |
| `IBaseRepository<T>` | Domain | OrderRepository | Veri erişimi soyutlaması |
| `IFixSession` | Application.Interfaces | QuickFixSession | FIX protokolü soyutlaması |

### Sağladığı Faydalar

1. **Test edilebilirlik**: Unit testlerde `IOrderService` ve `IBaseRepository` için mock verilebilir.
2. **Bağımlılık tersine çevrilmesi**: Üst katmanlar somut sınıflara değil interface’lere bağımlı.
3. **Kolay değiştirme**: Repository veya FIX implementasyonu değişse bile üst katmanlar aynı kalır.
4. **Katman izolasyonu**: Application, Persistence veya Infrastructure detaylarını bilmeden çalışır.

### Örnek Akış

```
TestController → IOrderService (interface)
                      ↓
                 OrderService (concrete)
                      ↓
                 IBaseRepository<DtoOrder> (interface)
                      ↓
                 OrderRepository (concrete)
```

---

## 5. DTO – Entity Ayrımı

### Neden Ayrım?

| Tip | Konum | Rol |
|-----|-------|-----|
| **DtoOrder** | Common | Hem katmanlar arası veri taşıma hem EF entity (FixSymbol tablosu) |

### DtoOrder (Tek Model)

```csharp
// Common/Dtos/Order/DtoOrder.cs
[Table("FixSymbol")]
public class DtoOrder : DtoBase
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
    [Column("data_type")]
    public int DataType { get; set; }
    // ...
}
```

- API, Application ve Persistence katmanlarında aynı model kullanılır.
- EF attribute'ları (`[Table]`, `[Column]`, `[Key]`) ile FixSymbol tablosuna eşlenir.
- OrderEntity ve OrderMapper kaldırılmıştır; tekrar önlenir.

---

## 6. Repository Pattern’in Rolü

### Genel Yapı

```
IBaseRepository<DtoOrder> (Domain)  ←  OrderRepository (Persistence)
```

- Domain sadece interface’i tanımlar.
- Persistence implementasyonu sağlar.

### IBaseRepository Metodları

```csharp
Task InsertAsync(T entity);
Task<T?> FetchByIdAsync(long id);
Task<List<T>> FetchAllAsync();
Task UpdateExistingAsync(long id, T entity);
Task RemoveByIdAsync(long id);
```

### OrderRepository Ne Yapar?

1. `DtoOrder` alır → `(direkt kullanım - mapping yok)` ile `OrderEntity`’ye çevirir.
2. Okuma işlemlerinde DtoOrder tipinde sonuç döner; ayrı mapping yoktur.
3. Okuma sonucunu `(direkt DtoOrder döner)` ile `DtoOrder`’a çevirir.

### Veri Akışı Örneği (Listeleme)

```
1. OrderService.RetrieveAllOrdersAsync()
2. → _orderRepository.FetchAllAsync()
3. → OrderRepository: _context.MarketData.ToListAsync() (DtoOrder listesi)
4. → List<DtoOrder> döner
6. → TestController: Ok(orders)
7. → JSON response
```

---

## 7. FixListenerWorker (Background Service)

### Konumu ve Rolü

- `API/BackgroundServices/FixListenerWorker.cs`
- `BackgroundService` base class kullanır.
- Uygulama açıldığında başlar, kapanana kadar çalışır.

### Çalışma Mantığı

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _fixSession.Start();                    // FIX oturumunu başlat
    while (!_fixSession.IsConnected && ...) // Bağlantı kurulana kadar bekle
        await Task.Delay(500, stoppingToken);
    await Task.Delay(Timeout.Infinite, ...); // Sürekli çalış
}
```

### DI ile Bağımlılık

- `IFixSession` constructor’da inject edilir.
- Implementasyon `QuickFixSession` (Infrastructure).
- `AddHostedService<FixListenerWorker>()` ile Singleton olarak çalışır.

### Sistemdeki Yeri

```
Uygulama Başlatılır
    → FixListenerWorker.ExecuteAsync() çalışır
    → IFixSession.Start() ile FIX bağlantısı kurulur
    → Uygulama kapanana kadar dinlemeye devam eder
```

- HTTP isteklerinden bağımsız.
- Order işlemleriyle doğrudan ilişkisi yok; sadece FIX bağlantısını yönetir.

---

## 8. Controller, Handler ve Service Görev Dağılımı

### Özet Tablo

| Bileşen | Konum | Rol | HTTP |
|---------|-------|-----|------|
| **TestController** | API | HTTP endpoint; IOrderService’e yönlendirir | Evet |
| **OrderHandler** | API | İç kullanım; IOrderService’e delegasyon | Hayır |
| **OrderService** | Application | İş mantığı; Repository kullanır | Hayır |

### TestController

- `[ApiController]`, `[Route("api/[controller]")]` ile HTTP API sağlar.
- `IOrderService` inject edilir; sadece servis çağrısı yapar.
- Örnek: `GET /api/Test/list` → `_orderService.RetrieveAllOrdersAsync()` → `Ok(orders)`.

### OrderHandler

- HTTP attribute’ları yok; API controller değil.
- İç servislerden veya diğer bileşenlerden çağrılmak için.
- `IOrderService`’e sadece delegasyon yapar; aynı metotlar farklı kullanım senaryosu için.

### OrderService

- İş kurallarının yazılabileceği katman.
- `IBaseRepository<DtoOrder>` kullanarak veri erişimi.
- HTTP veya transport detaylarından bağımsız.

### Veri Akışı Örneği (Sipariş Listeleme)

```
1. HTTP GET /api/Test/list
2. ASP.NET Core → TestController.GetAllOrders()
3. TestController → _orderService.RetrieveAllOrdersAsync()
4. OrderService → _orderRepository.FetchAllAsync()
5. OrderRepository → DB sorgusu (DtoOrder) → doğrudan döner
6. List<DtoOrder> geri döner
7. OrderService → OrderRepository’den gelen listeyi aynen döner
8. TestController → Ok(orders) → JSON response
```

---

## 9. Özet: Katmanlar ve Akış

| Adım | Katman | Bileşen | Eylem |
|------|--------|---------|-------|
| 1 | API | TestController | HTTP isteğini alır |
| 2 | API | TestController | IOrderService çağrısı yapar |
| 3 | Application | OrderService | İş mantığı (varsa) uygular |
| 4 | Application | OrderService | IBaseRepository çağrısı yapar |
| 5 | Persistence | OrderRepository | DtoOrder ile doğrudan EF işlemi |
| 6 | Persistence | OrderRepository | EF Core ile DB işlemi |
| 7 | Application | OrderService | DtoOrder listesini döner |
| 8 | API | TestController | HTTP response döner |

Bu yapı sayesinde katmanlar belirgin sınırlara sahiptir ve her katman kendi sorumluluğu ile sınırlı kalır.
