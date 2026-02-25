namespace FixTrading.Common.Exceptions;

// İş kuralları bozulduğunda fırlatılır
public class DomainException : Exception
{
    // Mesajı üst sınıfa gönderir
    public DomainException(string message)
        : base(message)
    {
    }
}
