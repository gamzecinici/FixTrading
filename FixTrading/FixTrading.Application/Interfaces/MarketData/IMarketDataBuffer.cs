namespace FixTrading.Application.Interfaces.MarketData;

// FIX'ten gelen market data'yı doğrudan DB'ye yazmak yerine
// önce bellekte (RAM) tutmak için kullanılan buffer arayüzü.
public interface IMarketDataBuffer
{
    // Geçerli (bid ve ask dolu) market data bilgisini buffer'a ekler.
    // Bu metod DB işlemi yapmaz, sadece memory'e veri ekler.
    void Add(string symbol, decimal bid, decimal ask);

    // Buffer'daki verileri hemen kalıcı depoya yazar.
    // FIX disconnect sırasında son verilerin kaybolmaması için çağrılır.
    void Flush();
}
