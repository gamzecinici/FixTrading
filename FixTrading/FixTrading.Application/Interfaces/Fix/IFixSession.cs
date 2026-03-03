namespace FixTrading.Application.Interfaces.Fix
{

    // Bu interface, FIX oturumunu yönetmek için kullanılan temel operasyonları tanımlar.
    public interface IFixSession
    {
        void Start();  //FIX bağlantısını başlatır. Bu metod, FIX sunucusuna bağlanmak için gerekli işlemleri yapar.
        void Stop();   //FIX bağlantısını durdurur. Bu metod, mevcut FIX oturumunu sonlandırır ve kaynakları serbest bırakır.
        bool IsConnected { get; }  //FIX bağlantısının durumunu kontrol eder. Eğer aktif bir FIX oturumu varsa true döndürür, aksi halde false döndürür.
        void Subscribe(string symbol);  //Verilen sembol için Market Data isteği gönderir.
    }
}
