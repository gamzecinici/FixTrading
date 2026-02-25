# FixTrading Projesi - Mimari Öğrenme Rehberi

Bu rehber, projenin mimari yapısını **öğrenme odaklı** ve **sade bir dille** anlatır. Her kavram: nedir, nerede kullanılır, neden kullanırız sorularıyla açıklanır.

---

## 1. Genel Bakış: Katmanlı Mimari

### Bu Nedir?
Projeyi birbirine bağlı katmanlara bölen bir yapıdır. Her katmanın kendi görevi vardır; bir katman diğerine "işini yaptırır".

### Projede Nasıl?
```
API (giriş) → Application (iş mantığı) → Persistence (veritabanı)
```

### Neden Böyle Yapıyoruz?
- Her parçanın ne yaptığı nettir.
- Bir yeri değiştirdiğimizde diğerleri bozulmaz.
- Yeni özellik eklemek kolaylaşır.

**Benzetme:** Restoran gibi: garson (API) sipariş alır, mutfak (Application) hazırlar, depo (Persistence) malzemeyi sağlar. Herkes kendi işine bakar.

---

## 2. Dependency Injection (DI)

### Bu Nedir?
Sınıfların ihtiyaç duyduğu başka sınıfları kendilerinin yaratmak yerine **dışarıdan almasıdır**. Uygulama başlarken "kim neye ihtiyaç duyuyor" listesi tutulur ve otomatik verilir.

### Projede Nerede Kullanılıyor?
`Startup.cs` içindeki `ConfigureServices` metodunda tüm servisler kaydedilir. `Program.cs` uygulama başlarken Startup'ı çağırır. Controller veya Service bir şeye ihtiyaç duyduğunda, constructor'a yazılır; sistem otomatik verir.

### Neden Kullanıyoruz?
- Sınıflar birbirine sıkı sıkıya bağlanmaz.
- Test yazmak kolaylaşır (sahte versiyonlar verilebilir).
- Değişiklik yapmak daha güvenli olur.

**Benzetme:** Evde çamaşır makinesi bozulunca kendin tamir etmek yerine tamirci çağırırsın. DI da "tamirciyi çağırmak" gibi: ihtiyaç olan şey dışarıdan gelir.

---

## 3. Startup ve ConfigureServices

### Bu Nedir?
`Startup.cs`, uygulama açılırken **hangi servislerin kaydedileceğini** ve **HTTP pipeline'ın nasıl kurulacağını** tanımlayan sınıftır. İki ana metodu vardır: `ConfigureServices` (servis kayıtları) ve `Configure` (Swagger, Authorization, Controller yönlendirmesi).

### Projede Nerede Kullanılıyor?
- **Startup.cs:** Tüm servis kayıtları ve pipeline ayarları burada yapılır.
- **Program.cs:** Uygulama başlarken Startup'ı oluşturur, `ConfigureServices` ve `Configure` metodlarını çağırır.

### Neden Kullanıyoruz?
1. **Düzen:** Servis kayıtları ve pipeline tek bir dosyada toplanır; Program.cs sade kalır.
2. **Okunabilirlik:** Başlangıç konfigürasyonu ayrı bir yerde; nerede ne yapıldığı net görülür.
3. **Geleneksel yapı:** ASP.NET Core projelerinde sık kullanılan klasik mimariye uyum sağlar.

**Benzetme:** Program.cs = uygulamayı başlatan düğme. Startup.cs = açılışta çalacak ayarların listesi. Düğmeye basınca liste okunur ve uygulanır.

**Uygulama başlarken sıra:**
1. Program.cs çalışır
2. Startup oluşturulur
3. `ConfigureServices` çağrılır → tüm servisler kaydedilir
4. `Configure` çağrılır → Swagger, Authorization, Controller ayarları yapılır
5. `app.Run()` ile uygulama dinlemeye başlar

---

## 4. AddScoped ve AddSingleton

### Bu Nedir?
DI ile kaydettiğimiz servislerin **ne kadar süre yaşayacağını** belirler.

| Tip | Ne Anlama Gelir? |
|-----|------------------|
| **AddScoped** | Her HTTP isteğinde yeni bir kopya oluşturulur. İstek bitince silinir. |
| **AddSingleton** | Uygulama boyunca tek bir kopya vardır. Hep aynı nesne kullanılır. |

### Projede Nerede Kullanılıyor?
- **AddScoped:** OrderService, OrderRepository, DbContext, OrderHandler
- **AddSingleton:** FixApp, IFixSession (FIX bağlantısı)

### Neden Farklı Kullanıyoruz?

**Scoped:** Veritabanı ile ilgili işlemler her istekte ayrı olmalı. Bir isteğin verisi diğerine karışmamalı. Bu yüzden her istekte yeni Service ve Repository oluşturulur.

**Singleton:** FIX bağlantısı gibi tek bir kanal yeterlidir. Sürekli yeni bağlantı açmak gereksiz ve maliyetlidir. Bu yüzden uygulama boyunca tek instance kullanılır.

**Benzetme:** Scoped = her müşteriye ayrı hesap (temiz ve izole). Singleton = restorandaki tek anahtar (herkes aynı kapıyı kullanır).

---

## 5. Interface (Arayüz)

### Bu Nedir?
Bir sınıfın **ne yapabileceğini** tarif eden, ama **nasıl yaptığını** söylemeyen bir sözleşmedir. "Şu metodlar var" der; içi boştur, implementasyon başka sınıfta yazılır.

### Projede Nerede Kullanılıyor?
- `IOrderService` → OrderService uygular
- `IBaseRepository<DtoOrder>` → OrderRepository uygular
- `IFixSession` → QuickFixSession uygular

### Neden Kullanıyoruz?
1. **Esneklik:** OrderService yerine başka bir implementasyon takabiliriz; üst katman fark etmez.
2. **Test:** Testte gerçek veritabanı yerine sahte (mock) sürüm verebiliriz.
3. **Bağımlılık azaltma:** Controller, "OrderService var" demez; "IOrderService var" der. Böylece somut sınıfa bağlı kalmaz.

**Benzetme:** Interface = "şoför olan biri" talebi. Arabayı kimin kullanacağı önemli değil, şoförlük yapabilsin yeter. Değiştirmek kolay.

---

## 6. Repository Pattern

### Bu Nedir?
Veritabanı işlemlerini **tek bir katmanda** toplayan yapıdır. Repository = "veritabanı ile konuşan aracı". Ekleme, silme, güncelleme, listeleme gibi işlemler burada yapılır.

### Projede Nerede Kullanılıyor?
`OrderRepository` sınıfı: `FixSymbol` tablosuna erişir. `IBaseRepository<DtoOrder>` interface'ini uygular.

### Neden Kullanıyoruz?
1. **Ayrım:** Service, SQL veya EF bilmeden sadece "listele", "ekle" der.
2. **Merkezileştirme:** Tüm veritabanı erişimi tek yerde toplanır.
3. **Değiştirilebilirlik:** Veritabanı değişirse (örn. PostgreSQL → MongoDB) sadece Repository değişir; üst katmanlar aynı kalır.

**Benzetme:** Repository = kütüphane görevlisi. Sen "X kitabını getir" dersin; nasıl bulacağı, rafa nasıl ulaşacağı onun işi.

---

## 7. DTO (Data Transfer Object)

### Bu Nedir?
Katmanlar arasında **taşınan veri paketidir**. Sadece alanlar içerir; iş mantığı yoktur. "Bu bilgileri A'dan B'ye götür" demektir.

### Projede Nerede Kullanılıyor?
`DtoOrder` sınıfı: Sipariş bilgilerini API, Application ve Persistence arasında taşır. Hem veri transferi hem de veritabanı tablosu (FixSymbol) eşlemesi için kullanılır.

### Neden Kullanıyoruz?
1. **Standart format:** Herkes aynı yapıda veri bekler.
2. **Güvenlik:** Veritabanındaki tüm alanları dışarı açmak zorunda değiliz; sadece gerekenleri DTO'ya koyarız.
3. **Basitlik:** API'den gelen JSON ile veritabanı kaydı aynı model üzerinden işlenir.

**Benzetme:** DTO = zarf. İçinde ne olduğu belli, taşınması kolay; kimse zarfı açmadan "bu sipariş verisi" diyebilir.

---

## 8. EF Core (Entity Framework Core)

### Bu Nedir?
C# ile veritabanı arasında köprü kuran bir kütüphanedir. SQL yazmak yerine C# nesneleri üzerinden sorgu yazarsın; EF bunu SQL'e çevirir.

### Projede Nerede Kullanılıyor?
**Sadece Persistence katmanında:** AppDbContext, OrderRepository içinde. API ve Application katmanlarında EF kodu yoktur.

### Neden Sadece Persistence'ta?

1. **Sorumluluk ayrımı:** Veritabanı detayları sadece Persistence'ın işi. Application "veri getir" der; nasıl getirildiğini bilmez.
2. **Bağımlılık azaltma:** Veritabanı teknolojisi değişirse (EF → Dapper vb.) sadece Persistence değişir.
3. **Test:** Application katmanını test ederken gerçek veritabanına ihtiyaç yok; sahte Repository verilir.

**Benzetme:** EF Core = mutfaktaki fırın. Yemek (veri) orada pişer. Garson (API) ve şef (Service) fırının nasıl çalıştığını bilmez; sadece yemeği alır.

---

## 9. Adım Adım: GET İsteği Geldiğinde Ne Olur?

Örnek: Kullanıcı `GET /api/Test/list` ile tüm siparişleri istiyor.

| Adım | Katman | Ne Olur? |
|------|--------|----------|
| 1 | **API** | İstek TestController'a gelir. |
| 2 | **API** | Controller, `IOrderService`'e "tüm siparişleri getir" der. |
| 3 | **Application** | OrderService, `IBaseRepository`'ye "FetchAllAsync" çağrısı yapar. |
| 4 | **Persistence** | OrderRepository, DbContext ile veritabanından `FixSymbol` tablosunu okur. |
| 5 | **Persistence** | Veriler DtoOrder listesi olarak döner. |
| 6 | **Application** | OrderService listeyi Controller'a iletir. |
| 7 | **API** | Controller, listeyi JSON'a çevirir ve HTTP yanıtı olarak gönderir. |

**Özet:** İstek girer → Controller servise sorar → Servis repository'ye sorar → Repository veritabanından alır → Geriye doğru aynı yoldan döner.

---

## 10. Controller, Handler ve Service Farkı

| Bileşen | Ne Yapar? | HTTP Endpoint Var mı? |
|---------|-----------|------------------------|
| **TestController** | Dışarıdan gelen HTTP isteklerini alır, Service'e iletir, cevabı döner. | Evet (api/Test/...) |
| **OrderHandler** | İç kullanım için. Service'e aynı işlemleri yaptırır; ama HTTP ile dışarı açılmaz. | Hayır |
| **OrderService** | İş mantığını uygular, Repository ile konuşur. HTTP'den habersizdir. | Hayır |

**Kısaca:** Controller = kapı, Handler = iç koridor, Service = asıl işi yapan oda.

---

## 11. FixListenerWorker (Arka Plan Servisi)

### Bu Nedir?
Uygulama açık kaldığı sürece arka planda çalışan bir görevdir. HTTP isteği beklemez; kendi döngüsünde çalışır.

### Projede Nerede Kullanılıyor?
`FixListenerWorker` sınıfı: FIX protokolü bağlantısını açar, dinlemeye devam eder. Uygulama kapanınca bağlantıyı kapatır.

### Neden Kullanıyoruz?
FIX gibi sürekli bağlı kalması gereken sistemler için. HTTP gibi "istek gelince cevap ver" değil; "sürekli dinle" mantığı vardır.

---

## 12. Özet Tablo: Kavramlar ve Rolleri

| Kavram | Kısa Açıklama |
|--------|----------------|
| **Katmanlı Mimari** | Her katmanın kendi görevi var; sırayla çalışırlar. |
| **DI** | Sınıflar ihtiyaçlarını dışarıdan alır; kendisi üretmez. |
| **Startup** | Servis kayıtları ve pipeline ayarları burada; Program.cs sadece başlatır. |
| **AddScoped** | Her istekte yeni kopya; veritabanı işleri için. |
| **AddSingleton** | Uygulama boyunca tek kopya; bağlantılar için. |
| **Interface** | "Ne yapılacak" tarifi; "nasıl" başka yerde. |
| **Repository** | Veritabanı ile konuşan katman; Ekle, Sil, Getir, Güncelle. |
| **DTO** | Katmanlar arası taşınan veri paketi. |
| **EF Core** | C# ile veritabanı arasında köprü; sadece Persistence'ta. |

---

## 13. Akış Şeması (Basit)

```
[Kullanıcı] 
    → HTTP isteği (örn: GET /api/Test/list)
        → [TestController] İsteği alır
            → [OrderService] "Tüm siparişleri getir" der
                → [OrderRepository] Veritabanından okur
                    → [PostgreSQL] FixSymbol tablosu
                ← [DtoOrder listesi] döner
            ← Liste OrderService'e gelir
        ← Liste Controller'a gelir
    ← JSON yanıt kullanıcıya gider
```

---

Bu rehber, mimarinin **mantığını** kavramak için hazırlandı. Detaylı kod örnekleri için proje dosyalarına bakılabilir.
