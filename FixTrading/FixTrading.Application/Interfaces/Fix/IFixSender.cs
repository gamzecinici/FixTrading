namespace FixTrading.Application.Interfaces.Fix
{
    public interface IFixSender
    {
        Task SendAsync(string fixMessage);
    }
}
