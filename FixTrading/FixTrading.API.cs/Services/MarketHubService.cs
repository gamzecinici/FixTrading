using FixTrading.API.Hubs;
using FixTrading.Application.Contracts;
using FixTrading.Application.Interfaces.MarketData;
using Microsoft.AspNetCore.SignalR;

namespace FixTrading.API.Services;

// Backend tarafında enstrüman (instrument) ile ilgili bir işlem olduğunda
// bunu anlık olarak frontend'e (tarayıcıya) SignalR üzerinden bildirmektir.
//Yeni bir parite eklendiğinde veya silindiğinde frontend'in güncel kalmasını sağlar.
public class MarketHubService : IMarketHubService
{
    //SignalR üzerinden istemcilere mesaj göndermeyi sağlayan yapı.
    private readonly IHubContext<MarketHub> _hubContext;

    // HubContext'i constructor üzerinden alır, böylece SignalR hub'ına erişebilir ve mesaj gönderebilir.
    public MarketHubService(IHubContext<MarketHub> hubContext)
    {
        _hubContext = hubContext;
    }

    //Yeni bir instrument eklendiğinde frontend'e bildirim göndermek için kullanılan metot.
    //kullanıcının ekranı yenilemeye ihtiyaç duymadan yeni enstrümanın bilgilerini almasını sağlar.
    public async Task NotifyInstrumentCreatedAsync(AdminSymbolListItemDto instrument, CancellationToken ct = default)
    {
        // Frontend camelCase bekledigi icin anonim nesne ile gonderiyoruz
        await _hubContext.Clients.All.SendAsync("InstrumentCreated", new
        {
            instrumentId = instrument.InstrumentId,
            limitId = instrument.LimitId,
            symbol = instrument.Symbol,
            minMid = instrument.MinMid,
            maxMid = instrument.MaxMid,
            maxSpread = instrument.MaxSpread
        }, ct);
    }

    // Bir instrument silindiğinde frontend'e bildirim göndermek için kullanılan metot.
    //kullanıcının ekranı yenilemeye ihtiyaç duymadan silinen enstrümanın bilgilerini almasını sağlar. 
    public async Task NotifyInstrumentDeletedAsync(Guid instrumentId, string symbol, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.SendAsync("InstrumentDeleted", new
        {
            instrumentId = instrumentId,
            symbol = symbol
        }, ct);
    }

    public async Task NotifySubscriptionRejectedAsync(string symbol, string reason, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.SendAsync("SubscriptionRejected", new
        {
            symbol = symbol,
            reason = reason
        }, ct);
    }


    // Bir instrument güncellendiğinde frontend'e bildirim göndermek için kullanılan metot.
    //kullanıcının ekranı yenilemeye ihtiyaç duymadan güncellenen enstrümanın bilgilerini almasını sağlar.
    public async Task NotifyInstrumentUpdatedAsync(AdminSymbolListItemDto instrument, CancellationToken ct = default)
    {
        // Frontend camelCase beklediği için anonim nesne ile gönderiyoruz
        await _hubContext.Clients.All.SendAsync("InstrumentUpdated", new
        {
            instrumentId = instrument.InstrumentId,
            limitId = instrument.LimitId,
            symbol = instrument.Symbol,
            minMid = instrument.MinMid,
            maxMid = instrument.MaxMid,
            maxSpread = instrument.MaxSpread
        }, ct);
    }
}
