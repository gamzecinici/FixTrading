using FixTrading.Common.Dtos.Alert;

namespace FixTrading.Domain.Interfaces;

// Bu arayüz, sistemde oluşan alert'leri bildirmek için kullanılan bir arayüzdür.
// NotifyAsync metodu, verilen DtoAlert nesnesini asenkron olarak bildirir.
public interface IAlertNotifier
{

    // NotifyAsync metodu, verilen DtoAlert nesnesini asenkron olarak bildirir.
    // ct parametresi, işlemin iptal edilip edilmeyeceğini kontrol etmek için kullanılan bir CancellationToken'dır 
    Task NotifyAsync(DtoAlert alert, CancellationToken ct = default);
}
