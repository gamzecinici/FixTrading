using FixTrading.Common.Dtos.Alert;

namespace FixTrading.Application.Interfaces.Alerts;

// Alert'lerin kalıcı depoya yazılması (ör. MongoDB).
public interface IAlertStore
{
    Task WriteAsync(DtoAlert alert);
}
