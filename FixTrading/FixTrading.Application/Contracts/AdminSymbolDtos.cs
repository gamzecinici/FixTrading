//Admin panelinde sembol ve fiyat limitlerini listelemek için kullanılan veri taşıma modelidir
//sadece veri tasimak icin kullanilir, is mantigi icermez
namespace FixTrading.Application.Contracts;

//Admin ekraninda sembol ve fiyat limitlerini listelemek icin kullanılan veri tasıma modelidir
public record AdminSymbolListItemDto(
    Guid InstrumentId,
    Guid LimitId,
    string Symbol,
    decimal MinMid,
    decimal MaxMid,
    decimal MaxSpread);

//Admin panelinde yeni bir sembol ve fiyat limitleri eklemek icin kullanilan veri tasıma modelidir
public record AddSymbolApiRequest(string Symbol, decimal MinMid, decimal MaxMid, decimal MaxSpread, string? Base, string? Quote);
