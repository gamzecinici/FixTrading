namespace FixTrading.Common.Dtos.Arbitrage;

// Arbitraj tablosunun tum satirlarini tek seferde UI'a tasimak icin kullanilan zarf DTO.
public sealed class ArbitrageSnapshotDto
{
    // Tabloyu olusturan tum satirlar (Instrument tablosundaki her sembol icin bir satir).
    public List<DtoArbitrageRow> Rows { get; set; } = [];

    // Sinyal esigi (yuzde). % > Threshold => SAT, % < -Threshold => AL. UI'da yardim metninde gosterilebilir.
    public decimal SignalThresholdPercent { get; set; } = 0.2m;
}
