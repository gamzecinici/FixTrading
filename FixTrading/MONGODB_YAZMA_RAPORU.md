# MongoDB Market Data Yazma – Kod ve Akış Raporu

Bu rapor, FIX market data verilerinin MongoDB'ye nasıl yazıldığını ve ilgili tüm kodların ne işe yaradığını açıklar.

---

## 1. GENEL AKIŞ

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ SPOTEX FIX sunucusu → MarketDataSnapshotFullRefresh (W) / Incremental (X)    │
│ QuickFIX FromApp → OnMessage → ProcessMarketData / ProcessGroup              │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ FixApp.Render(symbol, bid, ask)                                              │
│ • Symbol normalize: EUR/USD → EURUSD (aynı sembol tek key)                   │
│ • Konsol: Anlık real-time akış                                               │
│ • Buffer: bid/ask > 0 ise _marketDataBuffer.Add(symbol, bid, ask)            │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ MongoMarketDataBuffer                                                        │
│ • Add(): Symbol normalize, yeni DtoMarketData → ConcurrentBag'e ekle        │
│ • Timer (60 sn) → FlushBuffer() → TÜM kayıtları InsertMany → MongoDB        │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Özet:**
- **Konsol:** Her tick'te anlık akış (Mongo'dan bağımsız).
- **MongoDB:** 1 dakika boyunca gelen TÜM tick verileri biriktirilir, 60 sn sonunda toplu InsertMany ile yazılır.
- **Sembol normalizasyonu:** EUR/USD ve EURUSD aynı sembol olarak işlenir → MongoDB'ye her zaman slash'sız (EURUSD) yazılır.

---

## 2. İLGİLİ DOSYALAR VE GÖREVLERİ

### 2.1 Common Katmanı – Veri Modeli

| Dosya | Konum | Görevi |
|-------|-------|--------|
| **DtoMarketData.cs** | `FixTrading.Common/Dtos/MarketData/` | MongoDB `marketData` collection için veri modeli. |

**Alanlar:**
- `Symbol` – Enstrüman sembolü (her zaman slash'sız: EURUSD, XAUUSD)
- `Bid` – Alış fiyatı
- `Ask` – Satış fiyatı
- `Mid` – (Bid + Ask) / 2
- `Timestamp` – UTC zaman damgası
- `TimestampFormatted` – dd.MM.yyyy HH:mm (Türkiye saati, UTC+3)

---

### 2.2 Application Katmanı – Arayüz

| Dosya | Konum | Görevi |
|-------|-------|--------|
| **IMarketDataBuffer.cs** | `FixTrading.Application/Interfaces/MarketData/` | Market data buffer sözleşmesi. |

**Metod:** `void Add(string symbol, decimal bid, decimal ask)`

---

### 2.3 Infrastructure – FIX Parse ve Buffer Çağrısı

| Dosya | Konum | Görevi |
|-------|-------|--------|
| **FixApp.cs** | `FixTrading.Infrastructure/Fix/Sessions/` | Market data'yı alır, normalize eder, konsola yazar ve buffer'a gönderir. |

**Önemli metodlar:**

| Metod | İşlevi |
|-------|--------|
| `OnMessage(MarketDataSnapshotFullRefresh)` | W mesajını parse eder; `ProcessMarketData` → `Render` |
| `OnMessage(MarketDataIncrementalRefresh)` | X mesajını parse eder; `ProcessGroup` → `Render` |
| `NormalizeSymbol(symbol)` | `EUR/USD` → `EURUSD` (slash kaldır, büyük harf) |
| `Render(symbol, bid, ask)` | Symbol normalize eder, konsola yazar, bid/ask > 0 ise `_marketDataBuffer.Add` çağırır. |

**MongoDB ile ilgisi:** `Render` içinde `_marketDataBuffer.Add(symbol, bid, ask)` çağrılır. Symbol önceden normalize edildiği için buffer ve MongoDB hep tutarlı key kullanır.

---

### 2.4 Infrastructure – MongoDB Buffer ve Yazma

| Dosya | Konum | Görevi |
|-------|-------|--------|
| **MongoMarketDataBuffer.cs** | `FixTrading.Infrastructure/MongoDb/` | Tüm tick'leri bellekte biriktirir; 60 sn'de bir MongoDB'ye toplu yazar. |

**Sorumluluklar:**

| Öğe | Açıklama |
|-----|----------|
| `_buffer` | `ConcurrentBag<DtoMarketData>` – Gelen her tick ayrı kayıt olarak eklenir (üzerine yazılmaz) |
| `_flushTimer` | Her 60 saniyede `FlushBuffer` tetikler |
| `Add(symbol, bid, ask)` | Symbol normalize eder; bid/ask > 0 ise yeni DtoMarketData oluşturup `_buffer.Add()` ile ekler |
| `FlushBuffer` | `TryTake` ile bag'i tamamen boşaltır, tüm kayıtları `InsertMany(ordered: false)` ile MongoDB'ye yazar |
| `Dispose` | Timer'ı durdurur, kalan verileri son bir kez flush eder |

**MongoMarketDataOptions:** ConnectionString, DatabaseName, CollectionName, FlushIntervalSeconds.

---

### 2.5 API Katmanı – Konfigürasyon ve DI

| Dosya | Konum | Görevi |
|-------|-------|--------|
| **Startup.cs** | `FixTrading.API.cs/` | MongoClient, IMarketDataBuffer, FixApp kayıtları. |
| **appsettings.json** | `FixTrading.API.cs/` | MongoMarketData ayarları. |

**appsettings.json – MongoMarketData:**
- `ConnectionString` – mongodb://localhost:27017
- `DatabaseName` – FixTrading
- `CollectionName` – marketData
- `FlushIntervalSeconds` – 60

---

## 3. VERİ AKIŞI (ADIM ADIM)

| Adım | Olay | Kod Yeri |
|------|------|----------|
| 1 | SPOTEX market data mesajı gönderir (W/X) | FIX |
| 2 | QuickFIX FromApp → Crack → OnMessage | FixApp.cs |
| 3 | ProcessMarketData / ProcessGroup → bid, ask parse | FixApp.cs |
| 4 | `Render(symbol, bid, ask)` → `symbol = NormalizeSymbol(symbol)` | FixApp.cs |
| 5 | Konsola yazar; bid/ask > 0 ise `_marketDataBuffer.Add(symbol, bid, ask)` | FixApp.Render |
| 6 | `MongoMarketDataBuffer.Add` → symbol normalize, yeni DtoMarketData oluştur, `_buffer.Add()` | MongoMarketDataBuffer |
| 7 | 60 sn sonra Timer `FlushBuffer` çağırır | MongoMarketDataBuffer |
| 8 | `TryTake` ile buffer boşaltılır, `InsertMany(snapshot)` ile MongoDB'ye yazılır | MongoMarketDataBuffer.FlushBuffer |

---

## 4. ÖZET TABLO

| Dosya | Katman | MongoDB ile ilişkisi |
|-------|--------|----------------------|
| DtoMarketData.cs | Common | Doküman modeli |
| IMarketDataBuffer.cs | Application | Buffer sözleşmesi |
| FixApp.cs | Infrastructure | FIX parse + Normalize + Add çağrısı |
| MongoMarketDataBuffer.cs | Infrastructure | Buffer + InsertMany |
| Startup.cs | API | DI kayıtları |
| appsettings.json | API | MongoMarketData ayarları |

---

## 5. ÖNEMLİ KURALLAR

- **bid veya ask ≤ 0 ise** kayıt buffer'a eklenmez.
- **Sembol normalizasyonu:** `EUR/USD` ve `EURUSD` aynı sembol; her yerde slash'sız (EURUSD) kullanılır.
- **1 dakika boyunca gelen TÜM tick verileri** bellekte biriktirilir; flush anında toplu InsertMany ile MongoDB'ye yazılır.
- **InsertMany ordered=false** – Hata olsa bile diğer kayıtlar yazılmaya devam eder.
- **Hata durumunda** kayıtlar tekrar buffer'a geri eklenir, bir sonraki flush'ta tekrar denenir.
- **Timestamp** UTC; `TimestampFormatted` Türkiye saati (UTC+3), format: dd.MM.yyyy HH:mm.
- **Veri kaynağı:** SPOTEX'ten gelen W/X mesajları QuickFIX FromApp üzerinden `FixApp.OnMessage` ile işlenir.
- **Uygulama kapanırken** `Dispose` çağrılır → kalan veriler son bir kez flush edilir, veri kaybı önlenir.

---

*Rapor: MongoDB market data yazma kodları ve akış*
