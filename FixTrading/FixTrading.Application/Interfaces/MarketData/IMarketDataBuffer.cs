namespace FixTrading.Application.Interfaces.MarketData;

/// <summary>
/// Market data buffer arayüzü. FIX'ten gelen verileri bellekte tutar ve periyodik bulk insert yapar.
/// Doğrudan DB işlemi yapılmaz; sadece veri ekleme çağrısı yapılır.
/// </summary>
public interface IMarketDataBuffer
{
    /// <summary>
    /// Geçerli (bid ve ask dolu) veriyi buffer'a ekler. Eksik veri eklenmez.
    /// </summary>
    void Add(string symbol, decimal bid, decimal ask);
}
