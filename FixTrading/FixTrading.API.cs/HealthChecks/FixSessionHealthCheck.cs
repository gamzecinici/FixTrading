using FixTrading.Application.Interfaces.Fix;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FixTrading.API.HealthChecks;

// FIX oturumunun bağlı olup olmadığını kontrol eden özel Health Check.
public class FixSessionHealthCheck : IHealthCheck
{
    private readonly IFixSession _fixSession;

    public FixSessionHealthCheck(IFixSession fixSession)
    {
        _fixSession = fixSession;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isConnected = _fixSession.IsConnected;
        return Task.FromResult(
            isConnected
                ? HealthCheckResult.Healthy("FIX oturumu bağlı.")
                : HealthCheckResult.Unhealthy("FIX oturumu bağlı değil."));
    }
}
