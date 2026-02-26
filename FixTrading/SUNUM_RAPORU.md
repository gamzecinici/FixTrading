# FixTrading Projesi – Detaylı Teknik Sunum Raporu

Bu rapor, FixTrading uygulamasının mimari yapısını, her dosyanın işlevini ve FIX protokolü üzerinden veri çekilme sırasını detaylı şekilde açıklar.

---

## 1. PROJE MİMARİSİ VE KATMAN YAPISI

```
┌─────────────────────────────────────────────────────────────────┐
│                        FixTrading.API.cs                         │  ← Giriş noktası, HTTP, Background Service
├─────────────────────────────────────────────────────────────────┤
│                     FixTrading.Application                       │  ← İş mantığı, servisler
├─────────────────────────────────────────────────────────────────┤
│                 FixTrading.Infrastructure.cs                     │  ← FIX protokolü, dış servisler
├─────────────────────────────────────────────────────────────────┤
│                    FixTrading.Persistence                        │  ← Veritabanı erişimi (EF Core)
├─────────────────────────────────────────────────────────────────┤
│     FixTrading.Domain    │    FixTrading.Common                  │  ← Interface'ler │ DTO'lar
└─────────────────────────────────────────────────────────────────┘
```

**Veri akış yönü:** API → Application → Domain/Infrastructure/Persistence → Veritabanı / FIX sunucusu

---

## 2. DOSYA BAZLI AÇIKLAMALAR

### 2.1 API Katmanı (FixTrading.API.cs)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **Program.cs** | Uygulama giriş noktası. `WebApplication` oluşturur, `Startup` üzerinden servisleri kaydeder ve pipeline'ı yapılandırır. | ASP.NET Core uygulamalarının standart başlangıç dosyasıdır. Tüm konfigürasyonu `Startup`'a devreder. |
| **Startup.cs** | Tüm Dependency Injection kayıtlarını ve HTTP pipeline'ı tanımlar. Controller, Swagger, DbContext, FIX servisleri, HostedService burada kaydedilir. | Konfigürasyonu tek bir yerden yönetmek için. `ConfigureServices` ve `Configure` metodları ile ayarlar merkezi tutulur. |
| **BackgroundServices/FixListenerWorker.cs** | Arka planda çalışan servis. FIX bağlantısını başlatır, veritabanındaki `instruments` tablosundan sembolleri çeker ve her biri için FIX sunucusuna **subscribe** isteği gönderir. | Uygulama açılır açılmaz FIX bağlantısını kurmak ve sembollere otomatik abone olmak için. `BackgroundService` olduğu için API kapanana kadar sürekli çalışır. |
| **Controllers/TestController.cs** | Test ve geliştirme için HTTP endpoint'leri sunar. `GET /api/Test/db-test`, `GET /api/Test/list`, `POST /api/Test/add` vb. | Veritabanı bağlantısını ve Instrument CRUD işlemlerini test etmek için. Gerçek API kullanımına örnek olarak kullanılır. |
| **Controllers/InstrumentHandler.cs** | HTTP endpoint olmayan, iç kullanım için Instrument işlemlerini yöneten handler. `IInstrumentService`'e delegasyon yapar. | Dış API açmadan, uygulama içinde Instrument işlemlerini kullanmak gerektiğinde kullanılır. Gelecekte başka katmanlar bu handler üzerinden işlem yapabilir. |

---

### 2.2 Application Katmanı (FixTrading.Application)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **ApplicationServiceRegistration.cs** | `IInstrumentService` → `InstrumentService` DI kaydını yapar. `AddApplicationServices()` extension metodu sunar. | Application katmanı servislerini tek bir yerden kaydetmek için. |
| **Services/IInstrumentService.cs** | Instrument CRUD işlemleri için interface. `RetrieveAllInstrumentsAsync`, `CreateNewInstrumentAsync` vb. metotları tanımlar. | Bağımlılıkları tersine çevirmek (DIP) ve test edilebilirlik için. API katmanı repository'e değil bu interface'e bağımlıdır. |
| **Services/InstrumentService.cs** | `IInstrumentService` implementasyonu. `IInstrumentRepository` kullanarak veritabanı işlemlerini yönetir. | İş mantığını repository'den ayırmak için. Tüm Instrument operasyonları burada orkestre edilir. |
| **Interfaces/Fix/IFixSession.cs** | FIX oturumu için interface: `Start()`, `Stop()`, `IsConnected`, `Subscribe(symbol)`. | FIX altyapısını Infrastructure'dan soyutlamak için. Böylece API ve Worker, QuickFIX detaylarını bilmez. |

---

### 2.3 Infrastructure Katmanı (FixTrading.Infrastructure.cs)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **Fix/Sessions/QuickFixSession.cs** | `IFixSession` implementasyonu. `fix.cfg` dosyasını okur, QuickFIX/n kütüphanesiyle **SocketInitiator** oluşturur. `Start()` ile sunucuya bağlanır, `Subscribe()` ile `FixApp.Subscribe` çağrısı yapar. | FIX protokolü bağlantısını ve başlatma/durdurma işlemlerini yönetir. `fix.cfg` yolu `AppContext.BaseDirectory` ile çözülür, böylece output klasöründeki config bulunur. |
| **Fix/Sessions/FixApp.cs** | QuickFIX/n `IApplication` implementasyonu. FIX mesajlarını alır, parse eder, konsola yazdırır. `OnLogon`, `OnCreate`, `FromApp` gibi event'leri işler. `Subscribe()` ile **MarketDataRequest** mesajı gönderir. `OnMessage(MarketDataSnapshotFullRefresh)` ve `OnMessage(MarketDataIncrementalRefresh)` ile gelen bid/ask verilerini işleyip `Render()` ile `SYMBOL - BID / ASK` formatında konsola yazar. | FIX sunucusu ile iletişimin merkezidir. Gelen tüm uygulama mesajları bu sınıftan geçer. Bid/ask verileri burada işlenir. |
| **fix.cfg** | FIX oturum ayarları: sunucu adresi (192.54.136.152), port (8060), kullanıcı adı/şifre, DataDictionary (FIX44.xml), bağlantı tipi (initiator). | QuickFIX/n'in sunucuya bağlanmak için okuduğu konfigürasyon dosyasıdır. |
| **FIX44.xml** (API projesinde) | FIX 4.4 protokolü için veri sözlüğü. Mesaj alanlarını ve yapısını tanımlar. | QuickFIX/n mesajları doğrulamak ve parse etmek için bu dosyayı kullanır. |

---

### 2.4 Persistence Katmanı (FixTrading.Persistence)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **AppDbContext.cs** | EF Core `DbContext`. `DbSet<DtoInstrument>` (instruments tablosu) ve `DbSet<DtoTrade>` (trades tablosu) tanımlar. Entity kullanılmaz; doğrudan DTO'lar tablolara eşlenir. | Veritabanı ile C# nesneleri arasındaki köprüdür. Tüm veritabanı işlemleri bu context üzerinden yapılır. |
| **Repositories/InstrumentRepository.cs** | `IInstrumentRepository` implementasyonu. `AppDbContext.Instruments` üzerinden CRUD işlemleri yapar. | Veritabanı erişimini tek bir sınıfta toplar. Application katmanı bu repository üzerinden veriye erişir. |
| **Repositories/TradeRepository.cs** | `ITradeRepository` implementasyonu. `AppDbContext.Trades` üzerinden CRUD işlemleri yapar. | Trades tablosu için veri erişim katmanı. Şu an FIX akışında kullanılmaz; ileride trade kayıtları için kullanılabilir. |

---

### 2.5 Domain Katmanı (FixTrading.Domain)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **Interfaces/IInstrumentRepository.cs** | Instrument veritabanı işlemleri için interface. Guid tabanlı CRUD metodları tanımlar. | Persistence detaylarını uygulama katmanından gizlemek için. |
| **Interfaces/ITradeRepository.cs** | Trade veritabanı işlemleri için interface. | Trades tablosu için repository sözleşmesi. |
| **Interfaces/IBaseRepository.cs** | Genel repository interface'i (long id ile). Şu an kullanılmıyor; Instrument ve Trade Guid kullanıyor. | Gelecekte long id kullanan varlıklar için kullanılabilir. |

---

### 2.6 Common Katmanı (FixTrading.Common)

| Dosya | Görevi | Neden Var? |
|-------|--------|------------|
| **Dtos/Instrument/DtoInstrument.cs** | Instrument modeli. Id, Symbol, Description, TickSize ve audit alanları. Hem API/Application'da DTO hem de Persistence'ta EF entity olarak kullanılır. `instruments` tablosuna eşlenir. | Katmanlar arası veri taşımak ve veritabanı eşlemesi için tek model kullanılır. |
| **Dtos/Trade/DtoTrade.cs** | Trade modeli. Id, OrderId, FillQuantity, FillPrice, TradeTime ve audit alanları. `trades` tablosuna eşlenir. | Trade verilerini temsil eder. |
| **Dtos/Order/DtoBase.cs** | Ortak audit alanları: RecordDate, RecordUser, RecordCreateDate. Diğer DTO'lar bu sınıftan türer. | Ortak alanları tekrar etmemek için. |

---

## 3. FIX ÜZERİNDEN VERİ ÇEKİLME SIRASI (ADIM ADIM)

Aşağıdaki sıra, uygulama başladığında ve FIX sunucusundan piyasa verisi alınırken gerçekleşen olayları adım adım gösterir.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 1: Uygulama Başlatma                                                        │
└──────────────────────────────────────────────────────────────────────────────────┘
   Program.cs → Startup.ConfigureServices() → FixListenerWorker (HostedService) başlar

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 2: FIX Bağlantısının Kurulması                                              │
└──────────────────────────────────────────────────────────────────────────────────┘
   FixListenerWorker.ExecuteAsync()
      → _fixSession.Start()
         → QuickFixSession.Start()
            → SocketInitiator.Start()
               → fix.cfg okunur (192.54.136.152:8060, FINTECHEE/SPOTEX)
               → FIX sunucusuna TCP bağlantısı açılır

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 3: FIX Logon ve Kimlik Doğrulama                                            │
└──────────────────────────────────────────────────────────────────────────────────┘
   FixApp.ToAdmin() → LOGON mesajına Username/Password eklenir (FINTECHEE / fintechee123)
   FixApp.OnLogon() → Bağlantı başarılı, _session atanır

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 4: Sembollerin Veritabanından Alınması                                      │
└──────────────────────────────────────────────────────────────────────────────────┘
   FixListenerWorker
      → IServiceScopeFactory.CreateScope()
      → AppDbContext.Instruments (DbSet<DtoInstrument>)
         → .Select(i => i.Symbol).Where(s => s != "").Distinct().ToListAsync()
      → Sonuç: ["EURUSD", "USDTRY", "XAUSUD", ...]

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 5: Her Sembol İçin Market Data Subscribe İsteği                             │
└──────────────────────────────────────────────────────────────────────────────────┘
   Her symbol için:
      _fixSession.Subscribe(symbol)
         → QuickFixSession.Subscribe(symbol)
            → FixApp.Subscribe(symbol)
               → MarketDataRequest (FIX mesaj tipi V) oluşturulur:
                  - MDReqID: benzersiz ID
                  - SubscriptionRequestType: SNAPSHOT_PLUS_UPDATES
                  - NoMDEntryTypes: BID + OFFER (ask)
                  - Symbol: EURUSD (veya diğer sembol)
               → Session.SendToTarget(request, _session)
                  → FIX sunucusuna mesaj gönderilir

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 6: FIX Sunucusundan İlk Veri (Snapshot)                                     │
└──────────────────────────────────────────────────────────────────────────────────┘
   FIX sunucusu → MarketDataSnapshotFullRefresh (mesaj tipi W) gönderir
      → FixApp.FromApp() → Crack(message)
         → OnMessage(MarketDataSnapshotFullRefresh)
            → ProcessMarketData(symbol, message)
               → NoMDEntries grubundan BID ve OFFER fiyatları parse edilir
               → Render(symbol, bid, ask)
                  → Console: "EURUSD - 1.0823 / 1.0825"

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 7: Anlık Güncellemeler (Incremental)                                        │
└──────────────────────────────────────────────────────────────────────────────────┘
   FIX sunucusu → MarketDataIncrementalRefresh (mesaj tipi X) gönderir (fiyat değiştiğinde)
      → FixApp.FromApp() → Crack(message)
         → OnMessage(MarketDataIncrementalRefresh)
            → Her NoMDEntries grubu için ProcessGroup(symbol, group)
               → BID veya OFFER güncellemesi parse edilir
               → Render(symbol, bid, ask)
                  → Console: "EURUSD - 1.0824 / 1.0826" (güncel değer)

┌──────────────────────────────────────────────────────────────────────────────────┐
│  ADIM 8: Sürekli Çalışma                                                          │
└──────────────────────────────────────────────────────────────────────────────────┘
   FixListenerWorker → Task.Delay(Timeout.Infinite) ile uygulama kapanana kadar bekler
   FIX sunucusu sürekli MarketDataIncrementalRefresh gönderir → Konsola sürekli yeni fiyatlar yazılır
```

---

## 4. FIX MESAJ AKIŞ DİYAGRAMI

```
[Veritabanı: instruments tablosu]
        │
        │ Symbol listesi (EURUSD, USDTRY, ...)
        ▼
[FixListenerWorker]
        │
        │ Subscribe(symbol)
        ▼
[QuickFixSession] ───► [FixApp.Subscribe]
        │                      │
        │                      │ MarketDataRequest (FIX MsgType V)
        │                      ▼
        │              ┌───────────────────┐
        │              │   FIX Sunucusu    │
        │              │ (192.54.136.152)  │
        │              └───────────────────┘
        │                      │
        │                      │ MarketDataSnapshotFullRefresh (W)
        │                      │ MarketDataIncrementalRefresh (X)
        │                      ▼
        │              [FixApp.FromApp]
        │                      │
        │                      │ Crack → OnMessage
        │                      ▼
        │              [ProcessMarketData / ProcessGroup]
        │                      │
        │                      │ bid, ask
        │                      ▼
        │              [Render] ───► Console: "EURUSD - 1.0823 / 1.0825"
```

---

## 5. BAĞIMLILIK ENJEKSİYONU (DI) ÖZETİ

| Servis | Tip | Kullanım |
|--------|-----|----------|
| `IInstrumentService` | Scoped | Her HTTP isteği için yeni instance. TestController, InstrumentHandler kullanır. |
| `IInstrumentRepository` | Scoped | DbContext ile uyumlu, istek başına bir instance. |
| `ITradeRepository` | Scoped | Trades işlemleri için. |
| `FixApp` | Singleton | FIX mesajlarını işleyen tek instance. Tüm bağlantı boyunca aynı. |
| `IFixSession` (QuickFixSession) | Singleton | FIX oturumu tek; Start/Stop/Subscribe tüm uygulama için ortak. |
| `FixListenerWorker` | HostedService | Uygulama başında bir kez çalışır, arka planda sürekli aktif. |
| `AppDbContext` | Scoped | Her veritabanı işlemi için ayrı context. |

---

## 6. ÖZET TABLO: KİM NE YAPAR?

| Bileşen | Sorumluluk |
|---------|------------|
| **Program.cs** | Uygulamayı başlatır. |
| **Startup** | Tüm servisleri ve pipeline'ı yapılandırır. |
| **FixListenerWorker** | FIX'i başlatır, DB'den sembolleri alır, her biri için subscribe gönderir. |
| **QuickFixSession** | FIX bağlantısını yönetir (Start/Stop), Subscribe'ı FixApp'e iletir. |
| **FixApp** | FIX mesajlarını alır, MarketDataRequest gönderir, gelen bid/ask'i parse edip konsola yazar. |
| **fix.cfg** | FIX sunucu adresi, port, kullanıcı bilgileri. |
| **instruments tablosu** | Subscribe edilecek sembollerin kaynağı (EURUSD, USDTRY vb.). |
| **TestController** | Instrument CRUD için HTTP API. |
| **InstrumentService/Repository** | Instrument veritabanı işlemleri. |

---

## 7. KONSOL ÇIKTISI ÖRNEĞİ

Uygulama başladığında ve FIX verisi geldiğinde örnek konsol çıktısı:

```
FIX başlatıldı.
FIX bağlantısı hazır.
Instrument tablosundan subscribe: EURUSD
Subscribe gönderildi: EURUSD
Instrument tablosundan subscribe: USDTRY
Subscribe gönderildi: USDTRY
...
EURUSD - 1.0823 / 1.0825
USDTRY - 32.1234 / 32.1250
EURUSD - 1.0824 / 1.0826
...
```

---

*Rapor tarihi: Sunum için hazırlanmıştır.*
