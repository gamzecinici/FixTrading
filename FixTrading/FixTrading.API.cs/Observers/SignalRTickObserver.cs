using FixTrading.API.Hubs;
using FixTrading.Application.Interfaces.MarketData;
using FixTrading.Common.Dtos.MarketData;
using Microsoft.AspNetCore.SignalR;

namespace FixTrading.API.Observers;

// FIX'ten gelen her gecerli (limit ihlalsiz) tick'i SignalR araciligiyla
// tum bagli tarayicilara anlik olarak push eden Observer.
public class SignalRTickObserver : IMarketDataObserver
{
    // SignalR HubContext'i, MarketHub'a baglanarak istemcilere mesaj gondermek icin kullanilir.
    private readonly IHubContext<MarketHub> _hubContext;


    // Constructor, HubContext'i alir ve depolar. Bu sayede OnTick metodu her cagrildiginda HubContext kullanilarak istemcilere mesaj gonderilebilir.
    public SignalRTickObserver(IHubContext<MarketHub> hubContext)
    {
        _hubContext = hubContext;
    }


    // FIX'ten gelen her gecerli tick'i isleyen metod. Tick bilgilerini alir ve SignalR araciligiyla tum bagli istemcilere gonderir.
    public void OnTick(DtoMarketData tick)
    {
        if (tick.Bid <= 0 || tick.Ask <= 0) return;

        _ = Task.Run(async () =>                                 //Bekletmeden arka planda gönderim yapabilmek icin Task.Run kullanilir. Bu sayede OnTick metodu engellenmez
        {
            try
            {
                //Tum kulllanicilara "PriceUpdate" adli bir mesaj gonderilir. Mesaj iceriginde tick bilgileri bulunur. Bu sayede istemciler anlik olarak fiyat guncellemelerini alabilirler.
                await _hubContext.Clients.All.SendAsync("PriceUpdate", new
                {
                    symbol = tick.Symbol,
                    bid    = tick.Bid,
                    ask    = tick.Ask,
                    mid    = tick.Mid,
                    spread = tick.Spread
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Broadcast hatası: {ex.Message}");
            }
        });
    }
}
