namespace FixTrading.Application.Interfaces.Fix
{
    public interface IFixSession
    {
        void Start();
        void Stop();
        bool IsConnected { get; }
        void Subscribe(string symbol);
    }
}
