# Instrument Kodları – Detaylı Açıklama Raporu

Bu rapor, projede Instrument ile ilgili tüm kodların ne işe yaradığını ve nasıl çalıştığını açıklar.

---

## 1. GENEL BAKIŞ

Instrument, finansal piyasalarda işlem gören bir varlığı temsil eder (örn: EURUSD, USDTRY). Projede:

1. **Veritabanında** `instruments` tablosunda saklanır.
2. **FIX market data akışında** hangi sembollere subscribe olunacağını belirler.
3. **HTTP API** üzerinden CRUD işlemleri yapılabilir.

---

## 2. DOSYA DOSYA AÇIKLAMALAR

### 2.1 Common Katmanı – Veri Modeli

#### `FixTrading.Common/Dtos/Instrument/DtoInstrument.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | Instrument verisinin katmanlar arası taşınması ve veritabanı tablosuna eşlenmesi |
| **Kullanım** | Hem DTO (API/Application) hem de EF Core entity (Persistence) olarak kullanılır |
| **Tablo** | `instruments` |

**Alanlar:**
- `Id` (Guid) – Benzersiz kimlik
- `Symbol` (string) – Sembol adı (örn: EURUSD, USDTRY)
- `Description` (string) – Açıklama
- `TickSize` (decimal) – Minimum fiyat adımı
- `RecordDate`, `RecordUser`, `RecordCreateDate` – DtoBase’den gelen audit alanları

**Neden DTO + Entity birlikte?** Proje yapısında ayrı Entity kullanılmıyor; tüm tablolar doğrudan DTO ile eşleniyor. Bu sayede katmanlar arasında model dönüşümü yapılmıyor.

---

### 2.2 Domain Katmanı – Repository Arayüzü

#### `FixTrading.Domain/Interfaces/IInstrumentRepository.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | Instrument veritabanı işlemlerinin sözleşmesini tanımlamak |
| **Neden Interface?** | Persistence detayları Application’dan gizlenir; test ve değiştirilebilirlik sağlanır |

**Metodlar:**
- `InsertAsync(DtoInstrument dto)` – Yeni kayıt ekler
- `FetchByIdAsync(Guid id)` – Id ile tek kayıt getirir
- `FetchAllAsync()` – Tüm kayıtları listeler
- `UpdateExistingAsync(Guid id, DtoInstrument dto)` – Kayıt günceller
- `RemoveByIdAsync(Guid id)` – Kayıt siler

---

### 2.3 Application Katmanı – İş Mantığı

#### `FixTrading.Application/Services/IInstrumentService.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | Instrument CRUD işlemleri için servis arayüzü |
| **Bağımlılık** | API katmanı doğrudan repository’ye değil bu interface’e bağımlıdır |

**Metodlar:**
- `RetrieveAllInstrumentsAsync()` – Tüm instrument’ları getirir
- `RetrieveInstrumentByIdAsync(Guid id)` – Id ile tek instrument getirir
- `CreateNewInstrumentAsync(DtoInstrument instrument)` – Yeni instrument ekler
- `UpdateExistingInstrumentAsync(Guid id, DtoInstrument instrument)` – Instrument günceller
- `DeleteInstrumentByIdAsync(Guid id)` – Instrument siler

---

#### `FixTrading.Application/Services/InstrumentService.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | IInstrumentService implementasyonu; repository çağrılarını yönetir |
| **Bağımlılık** | IInstrumentRepository enjekte edilir |

**İşleyiş:** Her metod doğrudan repository metoduna delegasyon yapar. Ek iş mantığı (validasyon, dönüşüm vb.) ihtiyaç olursa burada eklenir.

---

#### `FixTrading.Application/ApplicationServiceRegistration.cs`

**Yapılan işlem:**
```csharp
services.AddScoped<IInstrumentService, InstrumentService>();
```
- Her HTTP isteği için yeni `InstrumentService` örneği oluşturulur (Scoped)
- DbContext ile aynı yaşam döngüsüne sahip olur

---

### 2.4 Persistence Katmanı – Veritabanı Erişimi

#### `FixTrading.Persistence/AppDbContext.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | EF Core veritabanı bağlamı; tablolara erişim noktası |

**Instrument ile ilgili tanım:**
```csharp
public DbSet<DtoInstrument> Instruments { get; set; } = null!;
```
- `instruments` tablosuna `DtoInstrument` ile erişilir
- LINQ sorguları bu DbSet üzerinden yazılır

---

#### `FixTrading.Persistence/Repositories/InstrumentRepository.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | IInstrumentRepository implementasyonu; gerçek veritabanı işlemleri |
| **Bağımlılık** | AppDbContext enjekte edilir |

**Metodların işleyişi:**

| Metod | Yapılan işlem |
|-------|----------------|
| `InsertAsync` | `_context.Instruments.AddAsync(dto)` → `SaveChangesAsync` |
| `FetchByIdAsync` | `_context.Instruments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)` |
| `FetchAllAsync` | `_context.Instruments.AsNoTracking().ToListAsync()` |
| `UpdateExistingAsync` | Mevcut kaydı bulur, `SetValues` ile günceller, `SaveChangesAsync` |
| `RemoveByIdAsync` | Kaydı bulur, `Remove` ile siler, `SaveChangesAsync` |

**Not:** Okuma işlemlerinde `AsNoTracking()` kullanılır; performans için EF tracking devre dışı bırakılır.

---

### 2.5 API Katmanı – HTTP ve Arka Plan

#### `FixTrading.API.cs/Controllers/TestController.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | Instrument CRUD işlemleri için HTTP API endpoint’leri |
| **Route** | `/api/Test/` |

**Endpoint’ler:**

| HTTP | Endpoint | Açıklama |
|------|----------|----------|
| GET | `/api/Test/db-test` | Veritabanı bağlantısını test eder; instrument sayısını döner |
| GET | `/api/Test/list` | Tüm instrument’ları JSON olarak döner |
| POST | `/api/Test/add` | Body’deki DtoInstrument ile yeni kayıt ekler |
| PUT | `/api/Test/update/{id}` | Belirtilen id’deki kaydı günceller |
| DELETE | `/api/Test/delete/{id}` | Belirtilen id’deki kaydı siler |

**Bağımlılık:** `IInstrumentService` enjekte edilir; tüm işlemler bu servise yönlendirilir.

---

#### `FixTrading.API.cs/Controllers/InstrumentHandler.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | HTTP olmayan, iç kullanım için Instrument işlemleri |
| **Kullanım** | Diğer servisler veya middleware’ler Instrument işlemi yapacaksa bu handler kullanılabilir |

**Metodlar:** TestController ile aynı işlemler; farkı HTTP attribute’ları yok, sadece `IInstrumentService` çağrıları var.

---

#### `FixTrading.API.cs/BackgroundServices/FixListenerWorker.cs`

| Özellik | Açıklama |
|---------|----------|
| **Amaç** | Uygulama başlarken FIX bağlantısını kurmak ve `instruments` tablosundaki sembollere subscribe olmak |
| **Tür** | `BackgroundService` – Arka planda sürekli çalışır |

**Instrument ile ilgili akış:**

1. FIX oturumu başlatılır (`_fixSession.Start()`)
2. Bağlantı kurulana kadar beklenir
3. `IServiceScopeFactory` ile yeni scope oluşturulur (DbContext Scoped olduğu için gerekli)
4. `AppDbContext.Instruments` üzerinden sorgu:
   ```csharp
   var symbols = await dbContext.Instruments
       .AsNoTracking()
       .Select(i => i.Symbol.Trim())
       .Where(s => s != "")
       .Distinct()
       .ToListAsync();
   ```
5. Her sembol için `_fixSession.Subscribe(symbol)` çağrılır
6. FIX sunucusu bu semboller için market data (bid/ask) göndermeye başlar

**Sonuç:** Veritabanındaki `instruments` tablosu, FIX’e hangi semboller için veri isteği gideceğini belirler.

---

#### `FixTrading.API.cs/Startup.cs`

**Instrument ile ilgili kayıtlar:**
```csharp
services.AddScoped<IInstrumentRepository, InstrumentRepository>();
services.AddScoped<InstrumentHandler>();
```
- Application katmanında `IInstrumentService` zaten `ApplicationServiceRegistration` içinde kayıtlı
- Persistence ve API katmanı Instrument bileşenleri burada DI container’a eklenir

---

## 3. VERİ AKIŞI DİYAGRAMI

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        instruments tablosu (PostgreSQL)                  │
│  id (Guid) | symbol | description | tick_size | record_date | ...        │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ DbSet<DtoInstrument>
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AppDbContext.Instruments                                               │
└─────────────────────────────────────────────────────────────────────────┘
                    │                                    │
                    │ InstrumentRepository               │ Doğrudan (FixListenerWorker)
                    ▼                                    ▼
┌──────────────────────────────┐           ┌─────────────────────────────────┐
│  InstrumentService           │           │  FixListenerWorker               │
│  (IInstrumentService)        │           │  Symbol listesi alır             │
└──────────────────────────────┘           │  Her biri için FIX Subscribe     │
                    │                      └─────────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
┌──────────────────┐   ┌────────────────────┐
│  TestController  │   │  InstrumentHandler  │
│  HTTP API        │   │  İç kullanım        │
└──────────────────┘   └────────────────────┘
```

---

## 4. ÖZET TABLO

| Bileşen | Katman | Görevi |
|---------|--------|--------|
| DtoInstrument | Common | Veri modeli; tablo eşlemesi |
| IInstrumentRepository | Domain | Veri erişim sözleşmesi |
| IInstrumentService | Application | İş mantığı sözleşmesi |
| InstrumentService | Application | CRUD orkestrasyonu |
| InstrumentRepository | Persistence | Veritabanı işlemleri |
| AppDbContext.Instruments | Persistence | Tablo erişim noktası |
| TestController | API | HTTP CRUD endpoint’leri |
| InstrumentHandler | API | İç kullanım handler |
| FixListenerWorker | API | Instrument sembollerini FIX’e subscribe eder |

---

## 5. ÖRNEK SENARYOLAR

### Senaryo 1: Yeni Instrument Eklemek
1. POST `/api/Test/add` → `TestController.AddInstrument`
2. → `InstrumentService.CreateNewInstrumentAsync`
3. → `InstrumentRepository.InsertAsync`
4. → `AppDbContext.Instruments.AddAsync` + `SaveChangesAsync`
5. Kayıt veritabanına yazılır

### Senaryo 2: FIX’e Otomatik Subscribe
1. Uygulama başlar → `FixListenerWorker.ExecuteAsync`
2. `dbContext.Instruments` → Symbol listesi (örn: EURUSD, USDTRY)
3. Her symbol için `_fixSession.Subscribe(symbol)` → FIX sunucusuna MarketDataRequest
4. FIX sunucusu bid/ask verisi göndermeye başlar
5. `FixApp` gelen veriyi parse edip konsola `EURUSD - 1.0823 / 1.0825` formatında yazar

---

*Rapor tarihi: Instrument kodları açıklama raporu*
