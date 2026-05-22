using FixTrading.Common.Dtos.Alert;

namespace FixTrading.Application.Interfaces.Alerts;

// Alert'lerin bildirilmesi (ör. e-posta).
public interface IAlertNotifier
{
    Task NotifyAsync(DtoAlert alert, CancellationToken ct = default);
}
