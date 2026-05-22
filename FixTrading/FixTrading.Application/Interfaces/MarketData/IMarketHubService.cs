using FixTrading.Application.Contracts;

namespace FixTrading.Application.Interfaces.MarketData;

//Bu interface, SignalR �zerinden UI taraf�na bildirim g�ndermek i�in kullan�l�r.
public interface IMarketHubService
{
    //Yeni bir enstruman olu�turuldu�unda UI'yi bilgilendirir.
    Task NotifyInstrumentCreatedAsync(AdminSymbolListItemDto instrument, CancellationToken ct = default);

    //Bir enstruman silindiginde UI'yi bilgilendirir.
    Task NotifyInstrumentDeletedAsync(Guid instrumentId, string symbol, CancellationToken ct = default);

    //FIX aboneligi sunucu (broker) tarafindan reddedildiginde UI'yi bilgilendirir.
    Task NotifySubscriptionRejectedAsync(string symbol, string reason, CancellationToken ct = default);

    //Bir enstruman g�ncellendiginde UI'yi bilgilendirir.
    Task NotifyInstrumentUpdatedAsync(AdminSymbolListItemDto instrument, CancellationToken ct = default);
}
