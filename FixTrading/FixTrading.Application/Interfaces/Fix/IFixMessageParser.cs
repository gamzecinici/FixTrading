namespace FixTrading.Application.Interfaces.Fix
{
    public interface IFixMessageParser
    {
        object Parse(string rawFixMessage);
    }
}
