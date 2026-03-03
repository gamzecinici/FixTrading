//Bu interface, ham FIX mesajlarını ayrıştırmak için kullanılan temel operasyonları tanımlar.

namespace FixTrading.Application.Interfaces.Fix
{
    public interface IFixMessageParser  //Bu interface, ham FIX mesajlarını ayrıştırmak için kullanılan temel operasyonları tanımlar.
    {
        object Parse(string rawFixMessage);
    }
}
