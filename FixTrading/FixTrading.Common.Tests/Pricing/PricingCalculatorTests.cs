using FixTrading.Common.Pricing;
using Xunit;

namespace FixTrading.Common.Tests.Pricing;


// PricingCalculator sınıfının unit testlerini içeren test sınıfı.
// Bu testler, PricingCalculator'ın Mid, Spread ve FromBidAsk metodlarının doğru çalıştığını doğrular.
public class PricingCalculatorTests
{
    [Theory]          //Aynı test birden fazla veri ile çalıştırılacak.
    [InlineData(100, 102, 101)]
    [InlineData(50.5, 51.5, 51)]
    [InlineData(0.0001, 0.0002, 0.00015)]
    [InlineData(99, 101, 100)]

    //Bid ve ask geçerliyse doğru ortalama dönmeli.
    public void Mid_ValidBidAsk_ReturnsCorrectAverage(double bid, double ask, double expected)
    {
        // Mid hesaplama metodunu çağırıyoruz
        // bid ve ask double olarak geliyor ama metod decimal istediği için cast ediyoruz
        var result = PricingCalculator.Mid((decimal)bid, (decimal)ask);
        Assert.Equal((decimal)expected, result);    //// Hesaplanan mid değeri beklenen değer ile aynı mı kontrol edilir
    }

    [Theory]
    [InlineData(0, 102)]
    [InlineData(100, 0)]
    [InlineData(-1, 102)]
    [InlineData(100, -1)]
    [InlineData(0, 0)]

    // Bid veya Ask geçersiz olduğunda Mid metodunun 0 döndürmesini test eder
    public void Mid_InvalidBidOrAsk_ReturnsZero(double bid, double ask)    
    {
        var result = PricingCalculator.Mid((decimal)bid, (decimal)ask);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(100, 102, 2)]
    [InlineData(50.5, 51.5, 1)]
    [InlineData(100, 100, 0)]
    [InlineData(99.99, 100.01, 0.02)]

    //// Spread hesaplamasının doğru çalıştığını test eder Bid ve ask geçerliyse doğru spread dönmeli.
    public void Spread_ValidBidAsk_ReturnsCorrectSpread(double bid, double ask, double expected)
    {
        var result = PricingCalculator.Spread((decimal)bid, (decimal)ask);
        Assert.Equal((decimal)expected, result);
    }

    [Theory]
    [InlineData(0, 102)]
    [InlineData(100, 0)]
    [InlineData(-5, 10)]
    [InlineData(10, -5)]
    [InlineData(0, 0)]

    // Bid veya Ask geçersiz olduğunda Spread metodunun 0 döndürmesini test eder
    public void Spread_InvalidBidOrAsk_ReturnsZero(double bid, double ask)
    {
        var result = PricingCalculator.Spread((decimal)bid, (decimal)ask);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(100, 102, 101, 2)]
    [InlineData(50, 60, 55, 10)]
    [InlineData(99.5, 100.5, 100, 1)]

    // FromBidAsk metodunun hem mid hem de spread'i aynı anda doğru hesapladığını test eder. Geçerli bid ve ask verildiğinde doğru mid ve spread döndürmeli.
    public void FromBidAsk_ValidBidAsk_ReturnsCorrectMidAndSpread(double bid, double ask, double expectedMid, double expectedSpread)
    {
        var (mid, spread) = PricingCalculator.FromBidAsk((decimal)bid, (decimal)ask);
        Assert.Equal((decimal)expectedMid, mid);
        Assert.Equal((decimal)expectedSpread, spread);
    }

    [Theory]
    [InlineData(0, 102)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(0, 0)]

    // FromBidAsk metodunun geçersiz bid veya ask verildiğinde mid ve spread'i sıfır olarak döndürdüğünü test eder. Geçersiz bid veya ask verildiğinde mid ve spread sıfır olmalı.
    public void FromBidAsk_InvalidBidOrAsk_ReturnsZeros(double bid, double ask)
    {
        var (mid, spread) = PricingCalculator.FromBidAsk((decimal)bid, (decimal)ask);
        Assert.Equal(0, mid);
        Assert.Equal(0, spread);
    }

    [Fact]   // FromBidAsk metodunun, Mid ve Spread metodlarıyla tutarlı sonuçlar verdiğini test eder.

    //// FromBidAsk metodunun Mid() ve Spread() metodlarıyla tutarlı çalıştığını test eder
    public void FromBidAsk_ResultConsistency_MatchesSeparateMidAndSpreadCalls()
    {
        //// Örnek bid ve ask değerleri
        var bid = 99.25m;
        var ask = 100.75m;

        // FromBidAsk metodunu çağırarak mid ve spread'i aynı anda hesaplıyoruz
        var (mid, spread) = PricingCalculator.FromBidAsk(bid, ask);

        //// Aynı değerleri Mid() ve Spread() metodlarıyla ayrı ayrı hesaplıyoruz
        var expectedMid = PricingCalculator.Mid(bid, ask);
        var expectedSpread = PricingCalculator.Spread(bid, ask);


        // FromBidAsk metodunun döndürdüğü mid ve spread değerlerinin, Mid() ve Spread() metodlarıyla ayrı ayrı hesaplanan değerlerle aynı olduğunu kontrol ediyoruz
        Assert.Equal(expectedMid, mid);
        Assert.Equal(expectedSpread, spread);
    }
}
