# 0 Referans (Kullanılmayan) Kod Raporu

Bu raporda, projede **hiçbir yerden referans almayan** (0 referans) bileşenler listelenmiş, ne işe yaradıkları ve silinirse ne olacağı açıklanmıştır.

---

## 1. Tamamen kullanılmayan – Silinebilir / Dikkatli kullanılmalı

### 1.1 ICacheService
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Application/Interfaces/Cache/ICacheService.cs` |
| **Ne yapar** | Redis benzeri bir cache servisi için interface (SetAsync, GetAsync, RemoveAsync) |
| **Kim kullanıyor** | Hiç kimse – implementasyon yok, hiçbir yerde inject edilmiyor |
| **Olmazsa ne olur** | Hiçbir şey değişmez; zaten kullanılmıyor |
| **Öneri** | İleride cache kullanacaksan tut; kullanmayacaksan silinebilir |

---

### 1.2 IFixMessageParser
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Application/Interfaces/Fix/IFixMessageParser.cs` |
| **Ne yapar** | Ham FIX mesajını parse etmek için interface |
| **Kim kullanıyor** | Hiç kimse – QuickFIX/n zaten mesajları parse ediyor |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | Gereksiz; silinebilir veya ileride özel parse için tutulabilir |

---

### 1.3 IFixSender
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Application/Interfaces/Fix/IFixSender.cs` |
| **Ne yapar** | FIX mesajı göndermek için interface |
| **Kim kullanıyor** | Hiç kimse – mesaj gönderimi FixApp ve QuickFIX üzerinden yapılıyor |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | Gereksiz; silinebilir veya ileride farklı FIX client için tutulabilir |

---

### 1.4 InstrumentHandler
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.API.cs/Controllers/InstrumentHandler.cs` |
| **Ne yapar** | Controller ile IInstrumentService arasında ara katman; Instrument CRUD işlemlerini yönetir |
| **Kim kullanıyor** | Startup’ta `AddScoped` ile kayıtlı ama hiçbir controller/endpoint constructor’da **InstrumentHandler** istemiyor. TestController doğrudan **IInstrumentService** kullanıyor |
| **Olmazsa ne olur** | Şu an zaten kullanılmadığı için hiçbir şey değişmez |
| **Öneri** | Mimari gereği tanımlı ama fiilen kullanılmıyor. TestController’ı InstrumentHandler üzerinden çalışacak şekilde değiştirirsen kullanılır; aksi halde silinebilir |

---

### 1.5 IBaseRepository<T>
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Domain/Interfaces/IBaseRepository.cs` |
| **Ne yapar** | Generic CRUD repository sözleşmesi (Insert, FetchById, FetchAll, Update, Remove) – `long` id kullanır |
| **Kim kullanıyor** | Hiç kimse – IInstrumentRepository ve ITradeRepository bu interface’i implement etmiyor; kendi metodları var ve `Guid` kullanıyorlar |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | Şu an gereksiz; silinebilir veya ileride ortak base repository yapısı kurulacaksa tutulabilir |

---

### 1.6 DomainException
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Common/Exception/DomainException.cs` |
| **Ne yapar** | İş kuralı ihlallerinde fırlatılmak üzere özel exception sınıfı |
| **Kim kullanıyor** | Hiç kimse – projede hiçbir yerde `throw new DomainException(...)` yok |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | İleride iş kuralları ve validasyon eklenecekse tut; şu an kullanılmıyorsa silinebilir |

---

### 1.7 AppLogger<T>
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Common/Logging/AppLogger.cs` |
| **Ne yapar** | ILogger sarmalayıcı; Info, Error, Warning metotları sunar |
| **Kim kullanıyor** | Hiç kimse – DI’a kayıtlı değil, hiçbir sınıf inject etmiyor |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | Proje `Console.WriteLine` ve standart ILogger kullanıyor; silinebilir veya ileride merkezi log formatı için kullanılabilir |

---

### 1.8 DateTimeExtensions (ToUnixTime, ToUtc)
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Common/Extensions/DateTimeExtensions.cs` |
| **Ne yapar** | Tarih extension metotları: ToUnixTime, ToUtc |
| **Kim kullanıyor** | Hiç kimse – `dateTime.ToUnixTime()` veya `dateTime.ToUtc()` çağrısı yok |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | İleride Unix timestamp veya UTC dönüşümü gerekecekse tut; aksi halde silinebilir |

---

### 1.9 SystemConstants
| Özellik | Değer |
|---------|-------|
| **Dosya** | `FixTrading.Common/Constants/SystemConstants.cs` |
| **Ne yapar** | Sabitler: SystemName, FixVersion, DefaultCurrency |
| **Kim kullanıyor** | Hiç kimse – bu sabitlere referans yok |
| **Olmazsa ne olur** | Hiçbir şey değişmez |
| **Öneri** | İleride UI veya raporlama için kullanılabilir; şu an silinebilir |

---

### 1.10 ITradeRepository ve TradeRepository
| Özellik | Değer |
|---------|-------|
| **Dosyalar** | `FixTrading.Domain/Interfaces/ITradeRepository.cs`, `FixTrading.Persistence/Repositories/TradeRepository.cs` |
| **Ne yapar** | Trade (işlem) tablosu için CRUD repository |
| **Kim kullanıyor** | Startup’ta kayıtlı ama hiçbir controller veya servis constructor’da **ITradeRepository** istemiyor |
| **Olmazsa ne olur** | Şu an trade API’si kullanılmadığı için hiçbir şey değişmez |
| **Öneri** | Trade özelliği gelecekte eklenecekse tut; şimdilik kullanılmıyor |

---

## 2. Özet tablo

| Bileşen | Kullanım | Silinebilir mi? | Olmazsa Ne Olur? |
|---------|----------|-----------------|------------------|
| ICacheService | Yok | Evet | Etki yok |
| IFixMessageParser | Yok | Evet | Etki yok |
| IFixSender | Yok | Evet | Etki yok |
| InstrumentHandler | DI’da var, kimse inject etmiyor | Evet | Etki yok |
| IBaseRepository | Yok | Evet | Etki yok |
| DomainException | Yok | Evet | Etki yok |
| AppLogger | Yok | Evet | Etki yok |
| DateTimeExtensions | Yok | Evet | Etki yok |
| SystemConstants | Yok | Evet | Etki yok |
| ITradeRepository / TradeRepository | DI’da var, kimse inject etmiyor | Evet (trade kullanılmayacaksa) | Etki yok |

---

## 3. Sonuç

Bu bileşenlerin hepsi **şu an için kullanılmıyor**. Proje şu haliyle:

- **Instrument** işlemleri → TestController → IInstrumentService → InstrumentRepository
- **Market data** → FixApp → IMarketDataBuffer, ILatestPriceStore
- **Latest Price API** → LatestPriceHandler → ILatestPriceStore

Yukarıdaki 0-referans kodlar ne bu akışta ne de başka bir yerde kullanılıyor. İleride kullanılacak tasarım parçaları mı, yoksa gereksiz kalıntılar mı olduğuna göre silinebilir veya tutulabilir.

---

*Rapor: 0 referans kod bileşenleri ve etkileri*
