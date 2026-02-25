namespace FixTrading.Common.Extensions;

// Tarih işlemleri için yardımcı metotlar
public static class DateTimeExtensions
{
    // Tarihi Unix zamanına çevirir
    public static long ToUnixTime(this DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }

    // Tarihi UTC formatına çevirir
    public static DateTime ToUtc(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime();
    }
}
