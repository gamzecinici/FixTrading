using Microsoft.AspNetCore.SignalR;

namespace FixTrading.API.Hubs;

// Tüm bagli istemcilere (Admin/User sayfaları) anlik fiyat verilerini push eden SignalR Hub'i.
// İstemciler "/hubs/market" endpoint'ine baglanir ve "PriceUpdate" olayını dinler.
public class MarketHub : Hub
{

    // Anlik fiyat guncellemelerini tüm baglı istemcilere ilet
}
