using LGUTreasury.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LGUTreasury.Services;

public sealed class MonthlyBillingBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyBillingBackgroundService> _logger;

    public MonthlyBillingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonthlyBillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Long-term tenant bill generation failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenantIds = await db.LongTermPayees
            .Where(t => t.IsActive && t.IsBillingEnabled)
            .Select(t => t.LongTermPayeeID)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LongTermBillingGenerator.GenerateBillsForTenantAsync(db, tenantId, cancellationToken);
        }
    }
}
