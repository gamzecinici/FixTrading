# FixTrading – Proje Yapısı ve Kod Raporu

Bu rapor, projedeki tüm klasörleri, dosyaları ve fonksiyonları detaylıca açıklar.

---

## GENEL MİMARİ

Proje **Clean Architecture** (Temiz Mimari) prensibiyle 6 katmana ayrılmıştır:

```
FixTrading/
├── FixTrading.API.cs/            → Sunum katmanı (HTTP API + arka plan servisleri)
├── FixTrading.Application/       → İş mantığı katmanı (servisler + interface'ler)
├── FixTrading.Infrastructure.cs/ → Altyapı katmanı (FIX protokolü + MongoDB)
├── FixTrading.Domain/            → Domain katmanı (repository sözleşmeleri)
├── FixTrading.Common/            → Paylaşılan katman (DTO'lar, sabitler, yardımcılar)
├── FixTrading.Persistence/       → Veritabanı katmanı (PostgreSQL + EF Core)
├── FixTrading.slnx               → Solution dosyası
└── *.md raporlar                 → Teknik dokümantasyon
```

**Katman bağımlılık yönü:**

```
API → Application → Domain
API → Infrastructure → Application → Domain
API → Persistence → Domain
Tüm katmanlar → Common
```

---

## VERİ AKIŞI ÖZETİ

```
SPOTEX Sunucusu
    │
    ▼ (TCP / FIX 4.4)
QuickFixSession._initiator   ←  fix.cfg ayarları
    │
    ▼ (Logon sonrası)
FixApp.FromApp()             ←  Her gelen mesaj buraya gelir
    │
    ├── 35=W → OnMessage(SnapshotFullRefresh) → ProcessMarketData() → Render()
    ├── 35=X → OnMessage(IncrementalRefresh)  → ProcessGroup()      → Render()
    └── 35=Y → OnMessage(RequestReject)       → Konsola hata yazar
                                                       │
                                              Render() │
                                                       │
                              ┌─────────────────────────┴──────────────────────┐
                              │                                                │
                        Console.WriteLine()                      _marketDataBuffer.Add()
                        (anlık, her tick)                         (ConcurrentBag'e ekler)
                                                                       │
                                                                  Timer (60 sn)
                                                                       │
                                                                       ▼
                                                              FlushBuffer()
                                                                       │
                                                                       ▼
                                                          MongoDB.InsertMany()
                                                     (1 dk boyunca biriken TÜM kayıtlar
                                                      toplu olarak yazılır)
```

---

## 1. FixTrading.API.cs (Sunum Katmanı)

Uygulamanın giriş noktası. HTTP endpoint'leri, DI yapılandırması ve arka plan servislerini barındırır.

### 1.1 Program.cs

Uygulamanın **başlangıç dosyası**. .NET uygulaması ilk çalıştığında bu dosya tetiklenir.

| Satır | Ne Yapar |
|-------|----------|
| `WebApplication.CreateBuilder(args)` | Web uygulaması builder'ını oluşturur (Kestrel sunucu, config, logging hazırlanır) |
| `new Startup(builder.Configuration)` | Startup sınıfını oluşturur, appsettings.json ayarlarını verir |
| `startup.ConfigureServices(builder.Services)` | Tüm servisleri DI container'a kaydeder |
| `builder.Logging.ClearProviders()` / `AddConsole()` | Varsayılan log sağlayıcıları temizleyip sadece konsol loglamayı açar |
| `startup.Configure(app)` | HTTP pipeline'ını yapılandırır |
| `app.Run()` | Uygulamayı başlatır, HTTP isteklerini dinlemeye başlar |

---

### 1.2 Startup.cs

Tüm DI kayıtlarının ve HTTP pipeline yapılandırmasının yapıldığı ana ayar dosyası.

#### `ConfigureServices(IServiceCollection services)` — Servisleri sisteme tanıtır:

| Kayıt | Ne Yapar |
|-------|----------|
| `services.AddControllers()` | API Controller'ları aktif eder |
| `services.AddEndpointsApiExplorer()` | Endpoint keşfini aktifleştirir (Swagger için) |
| `services.AddSwaggerGen()` | Swagger UI/doc oluşturur |
| `services.AddApplicationServices()` | Application katmanı servislerini kaydeder (extension method) |
| `services.AddDbContext<AppDbContext>(...)` | PostgreSQL bağlantısını DefaultConnection string'i ile kurar |
| `services.AddScoped<IInstrumentRepository, InstrumentRepository>()` | Her HTTP istek için yeni InstrumentRepository oluşturur |
| `services.AddScoped<ITradeRepository, TradeRepository>()` | Her HTTP istek için yeni TradeRepository oluşturur |
| `services.Configure<FixMarketDataOptions>(...)` | appsettings.json'daki FixMarketData bölümünü config sınıfına bağlar |
| `services.Configure<MongoMarketDataOptions>(...)` | appsettings.json'daki MongoMarketData bölümünü config sınıfına bağlar |
| `services.AddSingleton<MongoClient>(...)` | MongoDB bağlantısını kurar (uygulama boyunca tek instance) |
| `services.AddSingleton<IMarketDataBuffer, MongoMarketDataBuffer>()` | Market data buffer (FIX'ten gelen veriyi RAM'de tutar, 60 sn'de MongoDB'ye yazar) |
| `services.AddSingleton<FixApp>()` | FIX mesaj işleyici (tek instance, tüm FIX mesajları buraya gelir) |
| `services.AddSingleton<IFixSession, QuickFixSession>()` | FIX oturum yöneticisi |
| `services.AddScoped<InstrumentHandler>()` | Instrument işlem handler'ı |
| `services.AddHostedService<FixListenerWorker>()` | Arka planda sürekli çalışan FIX dinleyici servisi |

#### `Configure(WebApplication app)` — HTTP istek pipeline'ını yapılandırır:

| Satır | Ne Yapar |
|-------|----------|
| `app.UseSwagger()` | Swagger JSON endpoint'ini aktif eder (sadece Development) |
| `app.UseSwaggerUI()` | Swagger tarayıcı arayüzünü aktif eder |
| `app.UseAuthorization()` | Yetkilendirme middleware'ini ekler |
| `app.MapControllers()` | Controller route'larını eşler |

---

### 1.3 appsettings.json

Tüm uygulama ayarlarını tutan konfigürasyon dosyası.

| Bölüm | Değer | Açıklama |
|-------|-------|----------|
| `ConnectionStrings.DefaultConnection` | `Host=localhost;Port=5432;...` | PostgreSQL bağlantı bilgileri |
| `ConnectionStrings.MongoDb` | `mongodb://localhost:27017` | MongoDB bağlantı string'i |
| `FixMarketData.UseSlashSymbolFormat` | `false` | false = EURUSD formatı, true = EUR/USD formatı |
| `MongoMarketData.ConnectionString` | `mongodb://localhost:27017` | MongoDB bağlantı adresi |
| `MongoMarketData.DatabaseName` | `FixTrading` | MongoDB database adı |
| `MongoMarketData.CollectionName` | `marketData` | MongoDB collection adı |
| `MongoMarketData.FlushIntervalSeconds` | `60` | Buffer'dan MongoDB'ye yazma aralığı (saniye) |
| `Logging.LogLevel.Default` | `Information` | Varsayılan log seviyesi |

---

### 1.4 BackgroundServices/FixListenerWorker.cs

Uygulama açıldığında arka planda sürekli çalışan **hosted service**. FIX bağlantısını başlatır ve market data subscribe işlemlerini yapar.

#### Constructor

| Parametre | Ne Yapar |
|-----------|----------|
| `IFixSession fixSession` | FIX oturum yöneticisi (DI'dan gelir) |
| `IServiceScopeFactory scopeFactory` | Scoped servisler oluşturmak için factory (DB erişimi için gerekli) |

#### `ExecuteAsync(CancellationToken stoppingToken)` — Ana çalışma metodu:

| Adım | Ne Yapar |
|------|----------|
| `_fixSession.Start()` | FIX initiator'ı başlatır → SPOTEX'e TCP bağlantı açar |
| `while (!_fixSession.IsConnected)` döngüsü | Logon cevabı gelene kadar 500ms aralıklarla bekler |
| `waitCount % 6 == 0` kontrolü | Her ~3 saniyede konsola "bağlantı bekleniyor" mesajı yazar |
| `dbContext.Instruments...ToListAsync()` | PostgreSQL'den instruments tablosundaki tüm sembolleri çeker |
| `_fixSession.Subscribe(symbol)` döngüsü | Her sembol için SPOTEX'e MarketDataRequest (V) mesajı gönderir |
| `Task.Delay(Timeout.Infinite)` | Uygulama kapanana kadar bekler (market data FixApp.FromApp üzerinden gelir) |

#### `StopAsync(CancellationToken)` — Uygulama kapatılırken:

FIX oturumunu düzgünce kapatır (`_fixSession.Stop()`).

---

### 1.5 Controllers/TestController.cs

HTTP API endpoint'leri. Swagger üzerinden test edilebilir REST API.

| Endpoint | HTTP Method | Metot | Ne Yapar |
|----------|-------------|-------|----------|
| `/api/Test/db-test` | GET | `TestDatabaseConnection()` | PostgreSQL bağlantısını test eder, instrument sayısını döner |
| `/api/Test/list` | GET | `GetAllInstruments()` | Tüm instrument kayıtlarını JSON olarak döner |
| `/api/Test/add` | POST | `AddInstrument(DtoInstrument)` | Yeni instrument ekler |
| `/api/Test/update/{id}` | PUT | `UpdateInstrument(Guid, DtoInstrument)` | Mevcut instrument'ı günceller |
| `/api/Test/delete/{id}` | DELETE | `DeleteInstrument(Guid)` | Instrument'ı siler |

---

### 1.6 Controllers/InstrumentHandler.cs

İç kullanım için instrument işlemlerini yöneten handler. Controller değil, doğrudan kod içinden çağrılabilir.

| Metot | Ne Yapar |
|-------|----------|
| `RetrieveAllInstrumentsAsync()` | Tüm instrument'ları getirir |
| `RetrieveInstrumentByIdAsync(Guid)` | ID'ye göre tek instrument getirir |
| `CreateNewInstrumentAsync(DtoInstrument)` | Yeni instrument ekler |
| `UpdateExistingInstrumentAsync(Guid, DtoInstrument)` | Mevcut instrument'ı günceller |
| `DeleteInstrumentByIdAsync(Guid)` | Instrument siler |

---

## 2. FixTrading.Application (İş Mantığı Katmanı)

Uygulamanın iş kurallarını ve servis interface'lerini tanımlar. Hiçbir altyapı bağımlılığı yoktur.

### 2.1 ApplicationServiceRegistration.cs

Extension method. Application katmanındaki servisleri DI'a kaydeder.

| Kayıt | Ne Yapar |
|-------|----------|
| `services.AddScoped<IInstrumentService, InstrumentService>()` | Her HTTP istek için yeni InstrumentService örneği oluşturur |

---

### 2.2 Interfaces/Cache/ICacheService.cs

Redis veya benzeri bir cache servisi için sözleşme. **Henüz implement edilmemiş**, gelecekte kullanılmak üzere hazırlanmış.

| Metot | Ne Yapar |
|-------|----------|
| `SetAsync(key, value, expiry?)` | Cache'e veri yazar (opsiyonel süre sonu) |
| `GetAsync(key)` | Cache'ten veri okur |
| `RemoveAsync(key)` | Cache'ten veri siler |

---

### 2.3 Interfaces/Fix/IFixMessageParser.cs

FIX mesajlarını parse etmek için sözleşme.

| Metot | Ne Yapar |
|-------|----------|
| `Parse(string rawFixMessage)` | Ham FIX string'ini nesneye çevirir |

---

### 2.4 Interfaces/Fix/IFixSender.cs

FIX mesajı göndermek için sözleşme.

| Metot | Ne Yapar |
|-------|----------|
| `SendAsync(string fixMessage)` | FIX mesajını sunucuya gönderir |

---

### 2.5 Interfaces/Fix/IFixSession.cs

FIX oturum yönetimi sözleşmesi. QuickFixSession tarafından implement edilir.

| Üye | Ne Yapar |
|-----|----------|
| `Start()` | FIX bağlantısını başlatır |
| `Stop()` | FIX bağlantısını durdurur |
| `IsConnected` | Bağlantı durumunu döner (true/false) |
| `Subscribe(string symbol)` | Belirtilen sembol için market data isteği gönderir |

---

### 2.6 Interfaces/Instrument/IInstrumentService.cs

Instrument CRUD işlemleri için sözleşme.

| Metot | Ne Yapar |
|-------|----------|
| `RetrieveAllInstrumentsAsync()` | Tüm instrument'ları listeler |
| `RetrieveInstrumentByIdAsync(Guid)` | ID'ye göre instrument getirir |
| `CreateNewInstrumentAsync(DtoInstrument)` | Yeni instrument oluşturur |
| `UpdateExistingInstrumentAsync(Guid, DtoInstrument)` | Mevcut instrument'ı günceller |
| `DeleteInstrumentByIdAsync(Guid)` | Instrument'ı siler |

---

### 2.7 Interfaces/MarketData/IMarketDataBuffer.cs

Market data buffer'ı için sözleşme. MongoMarketDataBuffer tarafından implement edilir.

| Metot | Ne Yapar |
|-------|----------|
| `Add(string symbol, decimal bid, decimal ask)` | Bir tick verisini bellek buffer'ına ekler (DB'ye dokunmaz) |

---

### 2.8 Services/InstrumentService.cs

`IInstrumentService` implementasyonu. Repository pattern'i kullanarak PostgreSQL işlemlerini yönetir.

| Metot | Çağırdığı Repository Metodu |
|-------|----------------------------|
| `RetrieveAllInstrumentsAsync()` | `_instrumentRepository.FetchAllAsync()` |
| `RetrieveInstrumentByIdAsync(Guid)` | `_instrumentRepository.FetchByIdAsync(id)` |
| `CreateNewInstrumentAsync(DtoInstrument)` | `_instrumentRepository.InsertAsync(instrument)` |
| `UpdateExistingInstrumentAsync(Guid, DtoInstrument)` | `_instrumentRepository.UpdateExistingAsync(id, instrument)` |
| `DeleteInstrumentByIdAsync(Guid)` | `_instrumentRepository.RemoveByIdAsync(id)` |

---

## 3. FixTrading.Infrastructure.cs (Altyapı Katmanı)

FIX protokolü (QuickFIX/n) ve MongoDB ile ilgili tüm somut implementasyonlar burada yer alır.

### 3.1 fix.cfg

QuickFIX konfigürasyon dosyası. SPOTEX sunucusuna nasıl bağlanılacağını tanımlar.

| Ayar | Değer | Açıklama |
|------|-------|----------|
| `FileStorePath` | `datastore` | FIX session state'inin disk'te tutulduğu klasör |
| `FileLogPath` | `datalog` | FIX mesaj loglarının yazıldığı klasör |
| `ConnectionType` | `initiator` | Bu uygulama bağlantıyı başlatan taraf (client) |
| `HeartBtInt` | `30` | Her 30 saniyede heartbeat gönderir |
| `ReconnectInterval` | `30` | Bağlantı koparsa 30 sn sonra tekrar dener |
| `StartTime / EndTime` | `00:00:00 / 23:59:59` | 7/24 aktif |
| `Username / Password` | `FINTECHEE / fintechee123` | SPOTEX giriş bilgileri |
| `BeginString` | `FIX.4.4` | FIX protokol versiyonu |
| `SenderCompID` | `FINTECHEE` | Client kimliği |
| `TargetCompID` | `SPOTEX` | Server kimliği |
| `ResetOnLogon` | `Y` | Her login'de sequence numaralarını sıfırlar |
| `SocketConnectHost` | `192.54.136.152` | SPOTEX sunucu IP adresi |
| `SocketConnectPort` | `8060` | SPOTEX sunucu port numarası |
| `UseDataDictionary` | `Y` | FIX44.xml ile mesaj yapısı doğrulaması açık |
| `DataDictionary` | `FIX44.xml` | Kullanılacak FIX mesaj sözlüğü |

---

### 3.2 Fix/FixMarketDataOptions.cs

appsettings.json'daki `FixMarketData` bölümünü C# nesnesine eşleyen config sınıfı.

| Property | Varsayılan | Açıklama |
|----------|-----------|----------|
| `UseSlashSymbolFormat` | `true` | true → EUR/USD formatında gönderir, false → EURUSD formatında gönderir |

> **Not:** appsettings.json'da `false` olarak ayarlı. SPOTEX slash'sız format kullanıyor.

---

### 3.3 Fix/Sessions/FixApp.cs

**Projenin en kritik dosyası.** FIX sunucusundan gelen tüm mesajları yakalar, parse eder, konsola yazar ve MongoDB buffer'ına ekler.

`MessageCracker` + `IApplication` implement eder:
- **IApplication:** FIX yaşam döngüsü olaylarını yönetir (bağlantı açıldı/kapandı vs.)
- **MessageCracker:** Gelen mesaj tipine göre doğru OnMessage metodunu otomatik çağırır

#### Alanlar (Fields)

| Alan | Tipi | Ne Yapar |
|------|------|----------|
| `_session` | `SessionID?` | Aktif FIX oturumunun ID'si. null ise bağlantı yok |
| `_lock` | `object` | `_symbols` dictionary'sine thread-safe erişim sağlar |
| `_marketDataBuffer` | `IMarketDataBuffer` | MongoDB buffer'ına veri eklemek için |
| `_fixOptions` | `FixMarketDataOptions` | Sembol format ayarları |
| `_symbols` | `Dictionary<string, (decimal? Bid, decimal? Ask)>` | Her sembol için son bid/ask'ı tutar (konsol gösterimi için) |
| `_firstMarketDataLogged` | `bool` | İlk market data mesajı alındığında bir kez log yazmak için |
| `_marketDataMsgCount` | `int` | İlk 5 W/X mesajını loglamak, sonrasını sessizleştirmek için |

#### Metotlar

| Metot | Ne Yapar |
|-------|----------|
| `OnCreate(SessionID)` | FIX session nesnesi oluşturulduğunda çağrılır. Konsola "oturum oluşturuldu" yazar |
| `OnLogon(SessionID)` | SPOTEX Logon kabul ettiğinde çağrılır. `_session`'ı set eder → `IsConnected = true` olur |
| `OnLogout(SessionID)` | Bağlantı kapandığında çağrılır. `_session = null` yapar |
| `ToAdmin(Message, SessionID)` | Giden admin mesajlarını yakalar. Logon mesajına Username/Password ekler |
| `FromAdmin(Message, SessionID)` | Gelen admin mesajları (Heartbeat, Logon cevabı vs.). Şu an boş |
| `ToApp(Message, SessionID)` | Giden uygulama mesajları. Şu an boş |
| `FromApp(Message, SessionID)` | **Ana giriş noktası.** Gelen her mesajı yakalar. İlk 5 W/X mesajını loglar, sonra `Crack()` ile doğru OnMessage handler'ına yönlendirir |
| `Subscribe(string symbol)` | SPOTEX'e MarketDataRequest (35=V) gönderir. BID + OFFER ister, SNAPSHOT_PLUS_UPDATES modunda |
| `OnMessage(MarketDataSnapshotFullRefresh)` | 35=W mesajı. İlk tam fiyat bilgisi. `ProcessMarketData()`'ya yönlendirir |
| `OnMessage(MarketDataRequestReject)` | 35=Y mesajı. Subscribe reddedildiyse nedenini konsola yazar |
| `OnMessage(MarketDataIncrementalRefresh)` | 35=X mesajı. Anlık fiyat değişimleri. Her grup için `ProcessGroup()`'u çağırır |
| `ProcessMarketData(string, Message)` | W mesajındaki tüm grupları döner, bid ve ask fiyatları çıkarır, `Render()`'a gönderir |
| `ProcessGroup(string, Group)` | X mesajındaki tek bir grubu işler. Bid veya ask olduğunu belirleyip `Render()`'a gönderir |
| `NormalizeSymbol(string)` | EUR/USD → EURUSD dönüşümü (Trim + ToUpper + slash kaldır) |
| `Render(string, decimal?, decimal?)` | **Tüm akışın birleştiği nokta.** (1) Sembolü normalize eder, (2) `_symbols`'u günceller (lock ile thread-safe), (3) Konsola anlık fiyatı yazar, (4) Geçerli bid+ask varsa buffer'a ekler |

#### `Render` Metodu Detayı

```
Render(symbol, bid, ask)
    │
    ├─ 1. symbol = NormalizeSymbol(symbol)     → EUR/USD → EURUSD
    │
    ├─ 2. lock(_lock) {                        → Thread-safe erişim
    │      _symbols[symbol] güncelle            → Son bid/ask sakla
    │   }
    │
    ├─ 3. Console.WriteLine(...)               → Konsola anlık yazdır
    │
    └─ 4. if (bid > 0 && ask > 0)
           _marketDataBuffer.Add(...)          → MongoDB buffer'ına ekle
```

---

### 3.4 Fix/Sessions/QuickFixSession.cs

`IFixSession` implementasyonu. QuickFIX/n kütüphanesini sarmalayarak FIX bağlantısını yönetir.

#### Constructor

| Adım | Ne Yapar |
|------|----------|
| `Path.Combine(AppContext.BaseDirectory, "fix.cfg")` | bin/Debug klasöründeki fix.cfg dosyasının yolunu bulur |
| `new SessionSettings(configPath)` | fix.cfg'yi parse eder |
| `new FileStoreFactory(settings)` | FIX session state'ini disk'te saklayacak factory |
| `new FileLogFactory(settings)` | FIX mesajlarını disk'e loglayacak factory |
| `new SocketInitiator(...)` | TCP socket üzerinden SPOTEX'e bağlanacak initiator oluşturur |

#### Metotlar

| Metot | Ne Yapar |
|-------|----------|
| `IsConnected` | `FixApp.CurrentSession != null` ise true döner |
| `Start()` | `_initiator.Start()` → TCP bağlantısını açar, Logon gönderir |
| `Stop()` | `_initiator.Stop()` → Logout gönderir, bağlantıyı kapatır |
| `Subscribe(string symbol)` | `_app.Subscribe(symbol)` → FixApp'e yönlendirir |

---

### 3.5 MongoDb/MongoMarketDataBuffer.cs

`IMarketDataBuffer` implementasyonu. FIX'ten gelen **tüm** tick verilerini bellekte biriktirir ve 60 saniyede bir toplu olarak MongoDB'ye yazar.

#### Alanlar

| Alan | Tipi | Ne Yapar |
|------|------|----------|
| `_collection` | `IMongoCollection<DtoMarketData>` | MongoDB marketData collection'ına referans |
| `_buffer` | `ConcurrentBag<DtoMarketData>` | Thread-safe liste. 1 dakika boyunca gelen TÜM tick verilerini biriktirir |
| `_flushTimer` | `Timer` | Her 60 saniyede FlushBuffer'ı tetikler |
| `_flushIntervalMs` | `int` | Flush aralığı (milisaniye) |
| `_disposed` | `bool` | Dispose edilip edilmediğini takip eder |

#### Metotlar

| Metot | Ne Yapar |
|-------|----------|
| **Constructor** | MongoDB bağlantısını kurar, timer'ı başlatır. appsettings.json'dan ayarları okur |
| **`Add(symbol, bid, ask)`** | Bir tick verisini buffer'a ekler. bid/ask ≤ 0 ise atlar. Sembolü normalize eder. UTC + Türkiye saati hesaplar. DtoMarketData oluşturup `_buffer.Add()` ile ekler |
| **`FlushBuffer(object?)`** | Timer tarafından her 60 sn'de tetiklenir. `TryTake` ile bag'i tamamen boşaltır. Tüm kayıtları `InsertMany` ile tek seferde MongoDB'ye yazar. Hata olursa kayıtları tekrar buffer'a geri koyar |
| **`Dispose()`** | Timer'ı durdurur, kalan verileri son bir kez flush eder. Uygulama kapanırken DI tarafından çağrılır |

#### MongoMarketDataOptions (Config Sınıfı)

| Property | Varsayılan | Açıklama |
|----------|-----------|----------|
| `ConnectionString` | `mongodb://localhost:27017` | MongoDB sunucu adresi |
| `DatabaseName` | `FixTrading` | Database adı |
| `CollectionName` | `marketData` | Collection adı |
| `FlushIntervalSeconds` | `60` | Kaç saniyede bir flush yapılacağı |

---

## 4. FixTrading.Domain (Domain Katmanı)

En soyut katman. Sadece interface'ler içerir, hiçbir implementasyon yoktur.

### 4.1 Interfaces/IBaseRepository.cs

Tüm repository'ler için ortak CRUD sözleşmesi. Generic `<T>` ile çalışır.

| Metot | Ne Yapar |
|-------|----------|
| `InsertAsync(T entity)` | Yeni kayıt ekler |
| `FetchByIdAsync(long id)` | ID'ye göre tek kayıt getirir |
| `FetchAllAsync()` | Tüm kayıtları listeler |
| `UpdateExistingAsync(long id, T entity)` | Mevcut kaydı günceller |
| `RemoveByIdAsync(long id)` | Kaydı siler |

---

### 4.2 Interfaces/IInstrumentRepository.cs

Instrument tablosu için özelleştirilmiş repository sözleşmesi. Guid tabanlı ID kullanır.

| Metot | Ne Yapar |
|-------|----------|
| `InsertAsync(DtoInstrument)` | Yeni instrument ekler |
| `FetchByIdAsync(Guid)` | ID'ye göre instrument getirir |
| `FetchAllAsync()` | Tüm instrument'ları listeler |
| `UpdateExistingAsync(Guid, DtoInstrument)` | Instrument günceller |
| `RemoveByIdAsync(Guid)` | Instrument siler |

---

### 4.3 Interfaces/ITradeRepository.cs

Trade tablosu için repository sözleşmesi. Guid tabanlı ID.

| Metot | Ne Yapar |
|-------|----------|
| `InsertAsync(DtoTrade)` | Yeni trade ekler |
| `FetchByIdAsync(Guid)` | ID'ye göre trade getirir |
| `FetchAllAsync()` | Tüm trade'leri listeler |
| `UpdateExistingAsync(Guid, DtoTrade)` | Trade günceller |
| `RemoveByIdAsync(Guid)` | Trade siler |

---

## 5. FixTrading.Common (Paylaşılan Katman)

Tüm katmanlar tarafından kullanılan DTO'lar, sabitler, extension'lar ve yardımcı sınıflar.

### 5.1 Constants/SystemConstants.cs

Sistem genelinde kullanılan sabit değerler.

| Sabit | Değer | Açıklama |
|-------|-------|----------|
| `SystemName` | `"FixTrading Sistemi"` | Uygulama adı |
| `FixVersion` | `"FIX 4.4"` | Kullanılan FIX protokol versiyonu |
| `DefaultCurrency` | `"USD"` | Varsayılan para birimi |

---

### 5.2 Dtos/Instrument/DtoInstrument.cs

PostgreSQL `instruments` tablosuna eşlenen veri modeli. Hem DTO hem EF entity olarak kullanılır. `DtoBase`'den miras alır.

| Property | DB Kolonu | Tipi | Açıklama |
|----------|-----------|------|----------|
| `Id` | `id` | Guid | Primary key |
| `Symbol` | `symbol` | varchar(20) | Enstrüman sembolü (EURUSD, XAUUSD) |
| `Description` | `description` | varchar(100), nullable | Açıklama |
| `TickSize` | `tick_size` | numeric(18,8) | Minimum fiyat adımı |
| `RecordDate` | `record_date` | DateTime | Kayıt tarihi (DtoBase'den) |
| `RecordUser` | `record_user` | varchar(50), nullable | İşlemi yapan kullanıcı (DtoBase'den) |
| `RecordCreateDate` | `record_create_date` | DateTime | Oluşturulma tarihi (DtoBase'den) |

---

### 5.3 Dtos/MarketData/DtoMarketData.cs

MongoDB `marketData` collection'ına yazılan veri modeli.

| Property | Tipi | Açıklama |
|----------|------|----------|
| `Symbol` | string | Enstrüman sembolü (her zaman slash'sız: EURUSD) |
| `Bid` | decimal | Alış fiyatı |
| `Ask` | decimal | Satış fiyatı |
| `Mid` | decimal | Orta fiyat: (Bid + Ask) / 2 |
| `Timestamp` | DateTime | UTC zaman damgası (sorgulama/sıralama için) |
| `TimestampFormatted` | string | Türkiye saatiyle formatlanmış: dd.MM.yyyy HH:mm |

---

### 5.4 Dtos/Order/DtoBase.cs

Tüm DTO'lar için ortak audit (denetim) alanları. DtoInstrument ve DtoTrade bu sınıftan miras alır.

| Property | DB Kolonu | Açıklama |
|----------|-----------|----------|
| `RecordDate` | `record_date` | Kaydın tarihi |
| `RecordUser` | `record_user` | İşlemi yapan kullanıcı (max 50 karakter) |
| `RecordCreateDate` | `record_create_date` | Kaydın oluşturulma tarihi |

---

### 5.5 Dtos/Trade/DtoTrade.cs

PostgreSQL `trades` tablosuna eşlenen trade veri modeli. `DtoBase`'den miras alır.

| Property | DB Kolonu | Tipi | Açıklama |
|----------|-----------|------|----------|
| `Id` | `id` | Guid | Primary key |
| `OrderId` | `order_id` | long | İlişkili emir ID'si |
| `FillQuantity` | `fill_quantity` | decimal | Doldurulan miktar |
| `FillPrice` | `fill_price` | decimal | Doldurulan fiyat |
| `TradeTime` | `trade_time` | DateTime | İşlem zamanı |

---

### 5.6 Exception/DomainException.cs

İş kuralı ihlallerinde fırlatılan özel exception sınıfı.

| Constructor | Ne Yapar |
|-------------|----------|
| `DomainException(string message)` | Hata mesajını üst sınıfa (Exception) iletir |

---

### 5.7 Extensions/DateTimeExtensions.cs

DateTime için yardımcı extension metotlar.

| Metot | Ne Yapar |
|-------|----------|
| `ToUnixTime(this DateTime)` | Tarihi Unix timestamp'e çevirir (saniye cinsinden) |
| `ToUtc(this DateTime)` | Tarihi UTC formatına çevirir |

---

### 5.8 Logging/AppLogger.cs

Generic log wrapper sınıfı. `ILogger<T>`'yi sarmalayarak Türkçe prefix'li log yazmayı sağlar.

| Metot | Log Seviyesi | Format |
|-------|-------------|--------|
| `Info(string message)` | Information | "Bilgi: " + message |
| `Error(string message)` | Error | "Hata: " + message |
| `Warning(string message)` | Warning | "Uyarı: " + message |

---

## 6. FixTrading.Persistence (Veritabanı Katmanı)

PostgreSQL ile EF Core üzerinden iletişim kuran katman.

### 6.1 AppDbContext.cs

EF Core veritabanı bağlamı. PostgreSQL ile konuşur.

| Üye | Ne Yapar |
|-----|----------|
| `Instruments` | `DbSet<DtoInstrument>` → instruments tablosuna erişim |
| `Trades` | `DbSet<DtoTrade>` → trades tablosuna erişim |
| `OnModelCreating()` | Assembly'deki tüm EF konfigürasyonlarını otomatik uygular |

---

### 6.2 Repositories/InstrumentRepository.cs

`IInstrumentRepository` implementasyonu. instruments tablosuyla CRUD işlemleri yapar.

| Metot | Ne Yapar |
|-------|----------|
| `InsertAsync(DtoInstrument)` | AddAsync + SaveChangesAsync ile yeni kayıt ekler |
| `FetchByIdAsync(Guid)` | AsNoTracking ile ID'ye göre tek kayıt getirir |
| `FetchAllAsync()` | AsNoTracking ile tüm kayıtları listeler |
| `UpdateExistingAsync(Guid, DtoInstrument)` | Mevcut kaydı bulur, SetValues ile günceller, SaveChanges ile kaydeder |
| `RemoveByIdAsync(Guid)` | Kaydı bulur, Remove ile siler, SaveChanges ile uygular |

> **AsNoTracking nedir?** EF Core'un değişiklik takibini kapatır. Sadece okuma yapılacaksa performans kazancı sağlar.

---

### 6.3 Repositories/TradeRepository.cs

`ITradeRepository` implementasyonu. trades tablosuyla CRUD işlemleri yapar. InstrumentRepository ile aynı yapıdadır.

| Metot | Ne Yapar |
|-------|----------|
| `InsertAsync(DtoTrade)` | Trade ekler |
| `FetchByIdAsync(Guid)` | ID'ye göre trade getirir |
| `FetchAllAsync()` | Tüm trade'leri listeler |
| `UpdateExistingAsync(Guid, DtoTrade)` | Trade günceller |
| `RemoveByIdAsync(Guid)` | Trade siler |

---

## DOSYA-KATMAN ÖZET TABLOSU

| Dosya | Katman | Görevi |
|-------|--------|--------|
| Program.cs | API | Uygulamanın başlangıç noktası |
| Startup.cs | API | DI kayıtları ve HTTP pipeline |
| appsettings.json | API | Tüm konfigürasyon ayarları |
| FixListenerWorker.cs | API | Arka planda FIX dinleyici servisi |
| TestController.cs | API | HTTP REST API endpoint'leri |
| InstrumentHandler.cs | API | İç kullanım instrument handler |
| ApplicationServiceRegistration.cs | Application | Servis DI kayıtları |
| ICacheService.cs | Application | Cache sözleşmesi (henüz uygulanmadı) |
| IFixMessageParser.cs | Application | FIX mesaj parse sözleşmesi |
| IFixSender.cs | Application | FIX mesaj gönderme sözleşmesi |
| IFixSession.cs | Application | FIX oturum yönetim sözleşmesi |
| IInstrumentService.cs | Application | Instrument servis sözleşmesi |
| IMarketDataBuffer.cs | Application | Market data buffer sözleşmesi |
| InstrumentService.cs | Application | Instrument iş mantığı |
| fix.cfg | Infrastructure | QuickFIX bağlantı ayarları |
| FixMarketDataOptions.cs | Infrastructure | FIX sembol format ayarları |
| FixApp.cs | Infrastructure | FIX mesaj işleme, parse, konsol akışı, buffer ekleme |
| QuickFixSession.cs | Infrastructure | QuickFIX oturum yönetimi |
| MongoMarketDataBuffer.cs | Infrastructure | MongoDB buffer ve toplu yazma |
| IBaseRepository.cs | Domain | Ortak CRUD sözleşmesi |
| IInstrumentRepository.cs | Domain | Instrument repository sözleşmesi |
| ITradeRepository.cs | Domain | Trade repository sözleşmesi |
| SystemConstants.cs | Common | Sistem sabitleri |
| DtoInstrument.cs | Common | Instrument veri modeli (PostgreSQL) |
| DtoMarketData.cs | Common | Market data veri modeli (MongoDB) |
| DtoBase.cs | Common | Ortak audit alanları |
| DtoTrade.cs | Common | Trade veri modeli (PostgreSQL) |
| DomainException.cs | Common | Özel hata sınıfı |
| DateTimeExtensions.cs | Common | Tarih yardımcı metotları |
| AppLogger.cs | Common | Log wrapper sınıfı |
| AppDbContext.cs | Persistence | EF Core veritabanı bağlamı |
| InstrumentRepository.cs | Persistence | Instrument CRUD işlemleri (PostgreSQL) |
| TradeRepository.cs | Persistence | Trade CRUD işlemleri (PostgreSQL) |

---

*Rapor: FixTrading proje yapısı ve kod detayları*
