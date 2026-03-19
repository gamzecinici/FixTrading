using FixTrading.Common.Dtos.Alert;

namespace FixTrading.Domain.Interfaces;

/// <summary>
/// Alert bildirimi gönderen arayüz (e-posta, konsol vb.).
/// </summary>
public interface IAlertNotifier
{
    Task NotifyAsync(DtoAlert alert, CancellationToken ct = default);
}
