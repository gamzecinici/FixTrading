using FixTrading.Application.Interfaces.Pricing;
using FixTrading.Domain.Interfaces;

namespace FixTrading.Application.Services;

public sealed class PricingLimitsSyncService : IPricingLimitsSyncService
{
    private readonly IPricingLimitsRepository _repository;
    private readonly IPricingLimitsCache _cache;

    public PricingLimitsSyncService(IPricingLimitsRepository repository, IPricingLimitsCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task RefreshCacheFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var limits = await _repository.FetchAllAsync();
        _cache.UpdateLimits(limits);
    }
}
