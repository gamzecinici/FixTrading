using FixTrading.Common.Dtos.Alert;

namespace FixTrading.Domain.Interfaces;

//Bu interface, alert'lerin saklanmasıyla ilgili işlemleri tanımlar.
//Örneğin, limit ihlali olduğunda oluşturulan alert'lerin MongoDB "alerts" collection'ına yazılması için kullanılabilir.
public interface IAlertStore
{
    Task WriteAsync(DtoAlert alert);
}
