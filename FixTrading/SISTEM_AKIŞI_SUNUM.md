# FixTrading Sistem Akışı – Sunum Dökümanı

Bu döküman, sistemin nasıl çalıştığını, hangi dosyanın ne zaman devreye girdiğini adım adım açıklar.

---

## BÖLÜM 1: UYGULAMA BAŞLATILMASI

### Adım 1.1 – Giriş Noktası

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T0 | **Program.cs** | `dotnet run` ile uygulama başlar |
| T1 | Program.cs | `WebApplication.CreateBuilder(args)` → Kestrel web sunucusu, config, logging hazırlanır |
| T2 | Program.cs | `appsettings.json` yüklenir (ConnectionStrings, Redis, Mongo, FixMarketData) |
| T3 | Program.cs | `new Startup(builder.Configuration)` → Startup sınıfı oluşturulur |

---

### Adım 1.2 – Servis Kayıtları (ConfigureServices)

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T4 | **Startup.cs** | `ConfigureServices(services)` çağrılır |
| T5 | Startup.cs | `AddControllers()` → API controller'lar kaydedilir |
| T6 | Startup.cs | `AddEndpointsApiExplorer()` + `AddSwaggerGen()` → Swagger hazırlanır |
| T7 | Startup.cs | `AddApplicationServices()` → **ApplicationServiceRegistration.cs** çalışır; `IInstrumentService` → `InstrumentService` kaydedilir |
| T8 | Startup.cs | `AddDbContext<AppDbContext>` → PostgreSQL bağlantısı tanımlanır |
| T9 | Startup.cs | `IInstrumentRepository` → `InstrumentRepository`, `ITradeRepository` → `TradeRepository` kaydedilir |
| T10 | Startup.cs | `Configure<FixMarketDataOptions>`, `Configure<MongoMarketDataOptions>`, `Configure<RedisOptions>` → **appsettings.json**'daki ilgili bölümler okunur |
| T11 | Startup.cs | `IConnectionMultiplexer` (Redis) → `localhost:6379`'a bağlanmaya çalışır (AbortOnConnectFail=false; başarısız olsa da uygulama devam eder) |
| T12 | Startup.cs | `ILatestPriceStore` → `RedisLatestPriceStore` (Singleton) kaydedilir |
| T13 | Startup.cs | `MongoClient` → MongoDB bağlantısı hazırlanır |
| T14 | Startup.cs | `IMarketDataBuffer` → `MongoMarketDataBuffer` (Singleton) kaydedilir; **60 sn timer** başlar |
| T15 | Startup.cs | `FixApp` (Singleton) oluşturulur; `IMarketDataBuffer`, `ILatestPriceStore`, `FixMarketDataOptions` inject edilir |
| T16 | Startup.cs | `IFixSession` → `QuickFixSession` (Singleton) kaydedilir; **QuickFixSession** constructor'da **fix.cfg** okunur, `SocketInitiator` hazırlanır |
| T17 | Startup.cs | `InstrumentHandler`, `LatestPriceHandler` (Scoped) kaydedilir |
| T18 | Startup.cs | `FixListenerWorker` (HostedService) kaydedilir → uygulama başlayınca arka planda çalışacak |

---

### Adım 1.3 – Build ve Pipeline

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T19 | **Program.cs** | `var app = builder.Build()` → Tüm servisler derlenir, DI container hazırlanır |
| T20 | **Startup.cs** | `Configure(app)` çağrılır |
| T21 | Startup.cs | Konsola `[API] Web sunucu: http://localhost:5076` ve Swagger / Latest Price URL'leri yazılır |
| T22 | Startup.cs | `app.UseSwagger()`, `app.UseSwaggerUI()` → Swagger UI aktif edilir |
| T23 | Startup.cs | `app.MapControllers()` → TestController vb. controller route'ları eşlenir |
| T24 | Startup.cs | `app.MapGet("/api/LatestPrice", ...)` ve `app.MapGet("/api/LatestPrice/{symbol}", ...)` → Minimal API endpoint'leri tanımlanır |
| T25 | **Program.cs** | `app.Run()` → Kestrel HTTP dinlemeye başlar, HostedService'ler başlatılır |

---

## BÖLÜM 2: FIX BAĞLANTISI VE SUBSCRIBE

### Adım 2.1 – FixListenerWorker Başlar

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T26 | **FixListenerWorker.cs** | `ExecuteAsync()` çalışır (HostedService) |
| T27 | FixListenerWorker.cs | `_fixSession.Start()` çağrılır |

---

### Adım 2.2 – QuickFixSession ve FixApp

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T28 | **QuickFixSession.cs** | `Start()` → `_initiator.Start()` |
| T29 | QuickFixSession.cs | QuickFIX kütüphanesi **fix.cfg**'ye göre SPOTEX sunucusuna (192.54.136.152:8060) TCP bağlantısı açar |
| T30 | **fix.cfg** | Dosya okunur: ConnectionType=initiator, SocketConnectHost, SocketConnectPort, Username, Password, DataDictionary=FIX44.xml |
| T31 | **FixApp.cs** | QuickFIX `OnCreate(SessionID)` çağırır → konsola "FIX oturumu oluşturuldu." |
| T32 | FixApp.cs | Logon mesajı gönderilir; `ToAdmin()` ile Username/Password eklenir |
| T33 | FixApp.cs | SPOTEX Logon kabul eder → `OnLogon(SessionID)` çağrılır; `_session = sessionID` → `IsConnected = true` |
| T34 | FixListenerWorker.cs | `while (!_fixSession.IsConnected)` döngüsü biter; konsola "FIX bağlantısı hazır." |

---

### Adım 2.3 – Sembol Listesi ve Subscribe

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T35 | **FixListenerWorker.cs** | `_scopeFactory.CreateScope()` → yeni bir scope oluşturulur |
| T36 | FixListenerWorker.cs | `dbContext.Instruments` → **AppDbContext** (EF Core) kullanılarak PostgreSQL'deki **instruments** tablosundan semboller çekilir |
| T37 | **InstrumentRepository** / **AppDbContext** | `Select(i => i.Symbol).Distinct().ToListAsync()` → EURUSD, XAUUSD, USDTRY vb. |
| T38 | FixListenerWorker.cs | Her sembol için `_fixSession.Subscribe(symbol)` çağrılır |
| T39 | **QuickFixSession.cs** | `Subscribe(symbol)` → `_app.Subscribe(symbol)` |
| T40 | **FixApp.cs** | `Subscribe(symbol)` → `_session` null değilse, **MarketDataRequest (35=V)** FIX mesajı oluşturulur; BID + OFFER, SNAPSHOT_PLUS_UPDATES; `Session.SendToTarget(request, _session)` ile SPOTEX'e gönderilir |

---

## BÖLÜM 3: MARKET DATA AKIŞI (Her Tick)

### Adım 3.1 – FIX Mesajı Gelir

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T41 | QuickFIX (kütüphane) | SPOTEX'ten TCP üzerinden FIX mesajı gelir |
| T42 | **FixApp.cs** | `FromApp(Message message, SessionID sessionID)` çağrılır – her uygulama mesajı buraya düşer |
| T43 | FixApp.cs | `message.Header.GetString(Tags.MsgType)` → "W" (SnapshotFullRefresh) veya "X" (IncrementalRefresh) |
| T44 | FixApp.cs | `Crack(message, sessionID)` → mesaj tipine göre ilgili `OnMessage` metodu çağrılır |

---

### Adım 3.2 – Mesaj Parse (W veya X)

**W mesajı (Snapshot):**

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T45a | **FixApp.cs** | `OnMessage(MarketDataSnapshotFullRefresh message)` çağrılır |
| T46a | FixApp.cs | `ProcessMarketData(symbol, message)` → NoMDEntries grupları okunur, bid/ask çıkarılır |
| T47a | FixApp.cs | `Render(symbol, bid, ask)` çağrılır |

**X mesajı (Incremental):**

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T45b | **FixApp.cs** | `OnMessage(MarketDataIncrementalRefresh message)` çağrılır |
| T46b | FixApp.cs | Her NoMDEntries grubu için `ProcessGroup(symbol, group)` → bid veya ask parse edilir |
| T47b | FixApp.cs | `Render(symbol, bid, ask)` çağrılır |

---

### Adım 3.3 – Render: Konsol + Buffer + Redis

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T48 | **FixApp.cs** | `Render(symbol, bid, ask)` başlar |
| T49 | FixApp.cs | `symbol = NormalizeSymbol(symbol)` → EUR/USD → EURUSD |
| T50 | FixApp.cs | `lock (_lock)` ile `_symbols[symbol]` güncellenir (thread-safe) |
| T51 | FixApp.cs | `Console.WriteLine($"{symbol} - {bidText} / {askText}")` → **KONSOLA** anlık yazdırılır |
| T52 | FixApp.cs | `_marketDataBuffer.Add(symbol, bid, ask)` → **MongoMarketDataBuffer** |
| T53 | FixApp.cs | `_latestPriceStore.SetLatestAsync(symbol, bid, ask)` → **RedisLatestPriceStore** (fire-and-forget Task.Run) |

---

### Adım 3.4 – MongoDB Buffer (Her Tick)

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T54 | **MongoMarketDataBuffer.cs** | `Add(symbol, bid, ask)` çağrılır |
| T55 | MongoMarketDataBuffer.cs | `DtoMarketData` oluşturulur (Symbol, Bid, Ask, Mid, Timestamp, TimestampFormatted) |
| T56 | MongoMarketDataBuffer.cs | `_buffer.Add(dto)` → ConcurrentBag'e eklenir (1 dk boyunca tüm tick'ler birikir) |

---

### Adım 3.5 – Redis Yazma (Her Tick)

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T57 | **RedisLatestPriceStore.cs** | `SetLatestAsync(symbol, bid, ask)` çağrılır |
| T58 | RedisLatestPriceStore.cs | `DtoMarketData` JSON'a serialize edilir |
| T59 | RedisLatestPriceStore.cs | Redis'e `SET latest:price:{symbol} {json}` (SETEX ile TTL) |
| T60 | RedisLatestPriceStore.cs | `SADD latest:price:symbols {symbol}` → sembol listesi güncellenir |

---

### Adım 3.6 – MongoDB Flush (Her 60 Saniye)

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T61 | **MongoMarketDataBuffer.cs** | `_flushTimer` tetiklenir → `FlushBuffer(null)` çağrılır |
| T62 | MongoMarketDataBuffer.cs | `while (_buffer.TryTake(out var dto)) snapshot.Add(dto)` → buffer boşaltılır |
| T63 | MongoMarketDataBuffer.cs | `_collection.InsertMany(snapshot)` → MongoDB `marketData` collection'ına toplu yazılır |
| T64 | MongoMarketDataBuffer.cs | Konsola `[MongoMarketData] X kayıt yazıldı.` |

---

## BÖLÜM 4: LATEST PRICE API (HTTP İsteği)

### Adım 4.1 – GET /api/LatestPrice veya GET /api/LatestPrice/EURUSD

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T65 | Kestrel | HTTP isteği `http://localhost:5076/api/LatestPrice` veya `.../api/LatestPrice/EURUSD` olarak gelir |
| T66 | **Startup.cs** | `MapGet` ile tanımlı lambda çalışır |
| T67 | Startup.cs | `LatestPriceHandler` DI'dan resolve edilir (Scoped) |
| T68 | Lambda | `handler.GetAllLatestAsync()` veya `handler.GetLatestAsync(symbol)` çağrılır |

---

### Adım 4.2 – Handler ve Redis Okuma

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T69 | **LatestPriceHandler.cs** | `GetLatestAsync(symbol)` veya `GetAllLatestAsync()` |
| T70 | LatestPriceHandler.cs | `_latestPriceStore.GetLatestAsync(symbol)` / `GetAllLatestAsync()` |
| T71 | **RedisLatestPriceStore.cs** | `GetLatestAsync`: `GET latest:price:{symbol}` → JSON parse → `DtoMarketData` döner |
| T72 | RedisLatestPriceStore.cs | `GetAllLatestAsync`: `SMEMBERS latest:price:symbols` → her sembol için `GET latest:price:{symbol}` → liste döner |
| T73 | Lambda | `Results.Ok(price)` veya `Results.Ok(prices)` → JSON response HTTP 200 ile döner |

---

## BÖLÜM 5: TEST/INSTRUMENT API (HTTP İsteği)

### GET /api/Test/list, POST /api/Test/add vb.

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T74 | **TestController.cs** | `[HttpGet("list")]` vb. attribute ile route eşleşir |
| T75 | TestController.cs | `_instrumentService.RetrieveAllInstrumentsAsync()` çağrılır |
| T76 | **InstrumentService.cs** | `_instrumentRepository.FetchAllAsync()` |
| T77 | **InstrumentRepository.cs** | EF Core ile PostgreSQL `instruments` tablosundan veri çekilir |
| T78 | TestController.cs | `Ok(instruments)` → JSON response döner |

---

## BÖLÜM 6: UYGULAMA KAPANIRKEN

| Zaman | Dosya | Ne Olur |
|-------|-------|---------|
| T79 | Program.cs | Ctrl+C veya kapatma sinyali |
| T80 | **FixListenerWorker.cs** | `StopAsync()` → `_fixSession.Stop()` |
| T81 | **QuickFixSession.cs** | `_initiator.Stop()` → Logout, TCP kapatılır |
| T82 | **FixApp.cs** | `OnLogout(SessionID)` → `_session = null` |
| T83 | **MongoMarketDataBuffer.cs** | `Dispose()` → Timer durur, kalan buffer flush edilir |
| T84 | Kestrel | HTTP dinleme durur |

---

## BÖLÜM 7: DOSYA BAZLI ÖZET TABLO

| Dosya | Çalışma Zamanı | Görevi |
|-------|----------------|--------|
| **Program.cs** | Uygulama başında | Giriş noktası, builder, Build, Run |
| **appsettings.json** | Startup sırasında | ConnectionStrings, Redis, Mongo, FixMarketData config |
| **Startup.cs** | Başlangıç | ConfigureServices (DI), Configure (pipeline, MapGet) |
| **ApplicationServiceRegistration.cs** | ConfigureServices | IInstrumentService kaydı |
| **fix.cfg** | QuickFixSession constructor | FIX sunucu adresi, port, kullanıcı, DataDictionary |
| **FixListenerWorker.cs** | HostedService (sürekli) | FIX Start, sembol subscribe, bekleme |
| **QuickFixSession.cs** | FixListenerWorker.Start, Subscribe | FIX initiator, TCP bağlantı, Subscribe yönlendirme |
| **FixApp.cs** | Her FIX mesajında | FromApp, OnMessage, ProcessMarketData/ProcessGroup, Render, Buffer+Redis yazma |
| **MongoMarketDataBuffer.cs** | Her tick + her 60 sn | Add (buffer), FlushBuffer (InsertMany) |
| **RedisLatestPriceStore.cs** | Her tick (yazma), API isteğinde (okuma) | SetLatestAsync, GetLatestAsync, GetAllLatestAsync |
| **LatestPriceHandler.cs** | GET /api/LatestPrice isteğinde | GetLatestAsync, GetAllLatestAsync → ILatestPriceStore |
| **TestController.cs** | GET/POST /api/Test/* isteğinde | IInstrumentService üzerinden CRUD |
| **InstrumentService.cs** | TestController çağrısında | IInstrumentRepository delegasyonu |
| **InstrumentRepository.cs** | InstrumentService çağrısında | EF Core ile PostgreSQL erişimi |
| **AppDbContext.cs** | Repository kullanımında | DbSet<DtoInstrument>, DbSet<DtoTrade> |
| **DtoMarketData.cs** | Buffer ve Redis tarafında | Symbol, Bid, Ask, Mid, Timestamp modeli |
| **DtoInstrument.cs** | Instrument API ve DB tarafında | instruments tablosu modeli |

---

## BÖLÜM 8: VERİ AKIŞ ŞEMASI

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 1. UYGULAMA BAŞLANGICI                                                           │
│    Program.cs → Startup.ConfigureServices → Build → Configure → Run              │
│    FixListenerWorker.ExecuteAsync başlar                                         │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 2. FIX BAĞLANTISI                                                                │
│    FixListenerWorker → QuickFixSession.Start → fix.cfg → SPOTEX (TCP)            │
│    FixApp.OnCreate → OnLogon → _session set                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 3. SUBSCRIBE                                                                     │
│    FixListenerWorker → AppDbContext.Instruments (PostgreSQL) → sembol listesi    │
│    FixListenerWorker → _fixSession.Subscribe(symbol) × N                         │
│    FixApp.Subscribe → MarketDataRequest (V) → SPOTEX                             │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 4. HER TICK (W veya X mesajı)                                                    │
│    QuickFIX → FixApp.FromApp → Crack → OnMessage → ProcessMarketData/ProcessGroup│
│    FixApp.Render →                                                                 │
│      ├─ Console.WriteLine                                                        │
│      ├─ _marketDataBuffer.Add → MongoMarketDataBuffer (ConcurrentBag)            │
│      └─ _latestPriceStore.SetLatestAsync → RedisLatestPriceStore (Redis)         │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                    ┌───────────────────┴───────────────────┐
                    ▼                                       ▼
┌──────────────────────────────┐         ┌────────────────────────────────────────┐
│ 5a. MONGODB (60 sn timer)    │         │ 5b. REDIS (her tick)                    │
│ FlushBuffer → TryTake →      │         │ SET latest:price:{symbol}               │
│ InsertMany → marketData      │         │ SADD latest:price:symbols               │
└──────────────────────────────┘         └────────────────────────────────────────┘
                                                          │
                                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ 6. HTTP GET /api/LatestPrice veya /api/LatestPrice/{symbol}                      │
│    MapGet lambda → LatestPriceHandler → ILatestPriceStore                        │
│    RedisLatestPriceStore.GetLatestAsync / GetAllLatestAsync → Redis'ten oku      │
│    Results.Ok(json)                                                              │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

*Bu döküman sunum için hazırlanmıştır. Tüm dosyalar ve zamanlama adımları eksiksiz listelenmiştir.*
