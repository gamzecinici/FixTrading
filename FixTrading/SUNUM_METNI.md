# FixTrading Sunum Metni

Bu metin, projenin genel akışını sunumda anlatmak için kullanılabilir. Doğrudan okuyabileceğiniz düz cümleler halinde hazırlanmıştır.

---

## GİRİŞ

Merhaba. Bu sunumda FixTrading projesinin nasıl çalıştığını adım adım anlatacağım.

Projemiz, FIX protokolü üzerinden SPOTEX sunucusundan market verisi alan, bu veriyi konsola yazan, MongoDB ve Redis’e yazan, ayrıca HTTP API üzerinden sunan bir uygulama.

---

## UYGULAMA BAŞLANGICI

Uygulama çalıştığında ilk olarak Program.cs devreye giriyor.

Program.cs, Startup sınıfını kullanarak tüm servisleri sisteme kaydediyor. Bu kayıt işlemine Dependency Injection diyoruz. Yani hiçbir sınıfı elle new ile oluşturmuyoruz; sistem ihtiyaç duyduğunda kendisi oluşturuyor.

Startup içinde PostgreSQL, MongoDB, Redis bağlantıları kuruluyor. FixApp, MongoMarketDataBuffer, RedisLatestPriceStore gibi bileşenler Singleton olarak kaydediliyor. Yani uygulama boyunca tek bir örnek kullanılıyor.

Ardından FixListenerWorker adlı arka plan servisi başlıyor. Bu servis, FIX bağlantısını yöneten ana bileşen.

---

## FIX BAĞLANTISI

FixListenerWorker, QuickFixSession üzerinden FIX bağlantısını başlatıyor.

QuickFixSession, fix.cfg dosyasındaki ayarları okuyor. Bu dosyada SPOTEX sunucusunun adresi, portu, kullanıcı adı ve şifre var. QuickFIX kütüphanesi bu bilgilerle sunucuya TCP üzerinden bağlanıyor.

Bağlantı kurulunca FixApp içindeki OnLogon metodu çağrılıyor ve oturum aktif oluyor.

---

## SEMBOL SUBSCRIBE

Bağlantı hazır olduktan sonra PostgreSQL’deki instruments tablosundan semboller çekiliyor. Örneğin EURUSD, XAUUSD, USDTRY.

Her sembol için SPOTEX’e Market Data Request mesajı gönderiliyor. Yani sunucuya, bu sembollerin fiyatlarını göndermesini istiyoruz.

---

## MARKET DATA AKIŞI

SPOTEX’ten gelen her fiyat güncellemesi FixApp’in FromApp metoduna düşüyor.

Mesaj tipi W ise tam fiyat bilgisi, X ise sadece değişen fiyat geliyor. FixApp bu mesajları parse edip bid ve ask değerlerini çıkarıyor.

Her fiyat güncellemesi Render metoduna gidiyor. Render üç iş yapıyor.

Birincisi: Her tick’i anlık olarak konsola yazdırıyor. Yani kullanıcı piyasa verisini canlı takip edebiliyor.

İkincisi: Veriyi MongoMarketDataBuffer’a ekliyor. Bu buffer bir dakika boyunca gelen tüm verileri bellekte biriktiriyor.

Üçüncüsü: En son gelen fiyatı RedisLatestPriceStore üzerinden Redis’e yazıyor. Böylece her sembol için güncel fiyat Redis’te tutuluyor.

---

## MONGODB YAZMA

MongoMarketDataBuffer, gelen her tick’i ConcurrentBag adlı thread-safe bir listeye ekliyor. Yani bir dakika boyunca gelen tüm veriler burada birikiyor.

Her altmış saniyede bir Timer tetikleniyor ve FlushBuffer metodu çalışıyor. Bu metod, buffer’daki tüm kayıtları alıp MongoDB’ye InsertMany ile toplu yazıyor.

Böylece hem performanslı hem de yapılandırılmış bir şekilde geçmiş veri saklanmış oluyor.

---

## REDIS YAZMA

Her tick geldiğinde RedisLatestPriceStore’un SetLatestAsync metodu çağrılıyor. Bu metot, sembol başına en son fiyatı Redis’e yazıyor. Yani her yeni veri bir öncekinin üzerine yazılıyor.

Redis’te key olarak latest:price ve sembol adı kullanılıyor. Değer olarak da JSON formatında bid, ask, mid ve timestamp tutuluyor.

Redis çok hızlı olduğu için en son fiyata anında ulaşılabiliyor.

---

## LATEST PRICE API

Kullanıcı veya başka bir sistem en son fiyatı almak istediğinde HTTP isteği atıyor. İki endpoint var.

Birincisi: Tüm sembollerin en son fiyatları. Yani slash api LatestPrice.

İkincisi: Tek bir sembolün en son fiyatı. Yani slash api LatestPrice slash sembol adı.

Bu istekler Startup’ta MapGet ile tanımlı. Yani controller yok, doğrudan Minimal API kullanıyoruz.

İstek gelince LatestPriceHandler devreye giriyor. Handler, ILatestPriceStore üzerinden Redis’ten veriyi okuyup JSON olarak döndürüyor.

MongoDB’ye hiç bakmıyor. Sadece Redis’ten okuyor. Çünkü en son fiyat Redis’te, geçmiş veri MongoDB’de.

---

## INSTRUMENT API

Instrument işlemleri için TestController var. Bu controller IInstrumentService kullanıyor. Service de InstrumentRepository üzerinden PostgreSQL’deki instruments tablosuna erişiyor.

Listeleme, ekleme, güncelleme ve silme işlemleri bu katman üzerinden yapılıyor. Bu tablodaki semboller, FIX subscribe işleminde kullanılıyor.

---

## UYGULAMA KAPANIRKEN

Uygulama kapatıldığında FixListenerWorker’ın StopAsync metodu çağrılıyor. Bu da FIX bağlantısını düzgünce kapatıyor.

Ayrıca MongoMarketDataBuffer IDisposable implement ettiği için ASP.NET Core host, uygulama kapanırken otomatik olarak Dispose metodunu çağırıyor. Dispose içinde timer durduruluyor ve buffer’da kalan veriler son bir kez MongoDB’ye yazılıyor. Böylece veri kaybı olmuyor.

---

## MİMARİ ÖZET

Projemiz Clean Architecture prensibine uygun. API katmanı HTTP isteklerini alıyor. Application katmanı iş mantığını ve arayüzleri tanımlıyor. Infrastructure katmanı FIX, MongoDB ve Redis ile konuşuyor. Domain katmanı sadece sözleşmeleri içeriyor. Common katmanı DTO’lar ve paylaşılan modelleri tutuyor. Persistence katmanı PostgreSQL erişimini sağlıyor.

Tüm bileşenler Dependency Injection ile birbirine bağlı. Yani hiçbir sınıf başka sınıfı direkt new ile oluşturmuyor. Bu sayede test edilebilirlik ve esneklik artıyor.

---

## KISA ÖZET CÜMLELER

- Uygulama başlayınca FIX bağlantısı kuruluyor ve instruments tablosundaki semboller için subscribe gönderiliyor.
- Gelen her fiyat konsola yazılıyor, buffer’a ekleniyor ve Redis’e yazılıyor.
- Her altmış saniyede buffer’daki tüm veriler MongoDB’ye toplu yazılıyor.
- Latest Price API isteği gelince Redis’ten en son fiyat okunup döndürülüyor.
- Uygulama kapanırken bağlantılar kapatılıyor ve kalan veri MongoDB’ye yazılıyor.

---

*Bu metin sunumda doğrudan okunabilir veya slayt notu olarak kullanılabilir.*
