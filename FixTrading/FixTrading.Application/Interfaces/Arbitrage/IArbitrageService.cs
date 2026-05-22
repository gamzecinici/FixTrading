using FixTrading.Common.Dtos.Arbitrage;
using FixTrading.Common.Dtos.Instrument;
using FixTrading.Common.Dtos.MarketData;
using System.Collections.Generic;

namespace FixTrading.Application.Interfaces.Arbitrage;

//2 pariteyi karsilastirarak arbitraj firsatlarini hesaplayan servis. Tum pariteler icin snapshot olusturur ve kullanicinin sectigi karsilari yeniden hesaplar.
public interface IArbitrageService
{
    // Tum ana pariteler icin satirlari olusturur. Her satira uygun karsi parite listesi, default seciminin
    // hesaplanmis degerleri (beklenen, piyasa, fark, sinyal) yazilir.
    ArbitrageSnapshotDto BuildSnapshot(
        IReadOnlyList<DtoInstrument> instruments,
        IReadOnlyList<DtoMarketData> prices,
        Dictionary<string, string>? config = null);

    // Kullanicinin dropdown'dan sectigi karsi parite bilgisiyle tek bir satiri yeniden hesaplar.
    // counterSymbol uygun degilse (ortak para birimi yoksa) fallback olarak default mantigi uygulanir.
    DtoArbitrageRow Compute(
        string mainSymbol,
        string counterSymbol,
        IReadOnlyList<DtoInstrument> instruments,
        IReadOnlyList<DtoMarketData> prices,
        Dictionary<string, string>? config = null);
}
