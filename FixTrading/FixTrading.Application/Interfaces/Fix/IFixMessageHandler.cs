using FixTrading.Common.Dtos.MarketData;

namespace FixTrading.Application.Interfaces.Fix;

//Bu arayüz, FIX mesajlarını işlemekle ilgili işlemleri tanımlar.
//FIX' ten gelen veriyi alıp işleyip doğru şekilde observer'lara bildirim göndermekle ilgili bir yapı.
public interface IFixMessageHandler
{

    //FIX mesajlarını işlemek için kullanılan metot.
    void Handle(DtoMarketData tick);
}
