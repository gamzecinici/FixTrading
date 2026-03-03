//bu interface, FIX mesajlarını göndermek için kullanılan temel operasyonları tanımlar.
//SendAsync metodu, asenkron olarak bir FIX mesajını göndermek için kullanılır.
//Bu metod, gönderilen mesajın başarılı bir şekilde iletilip iletilmediğini kontrol etmek için Task döndürür.
namespace FixTrading.Application.Interfaces.Fix
{
    public interface IFixSender  //Bu interface, FIX mesajlarını göndermek için kullanılan temel operasyonları tanımlar.
    {
        Task SendAsync(string fixMessage);
    }
}
