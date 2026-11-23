using Backend.Application.Commands.Groups;
using Backend.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Backend.Infrastructure.Persistence;

internal sealed class GroupPurchaseExpirationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<GroupPurchaseExpirationService> logger
) : BackgroundService {
    private readonly Counter m_Failures = Metrics.CreateCounter(
        "vmeste_expiration_failures_total", "Failed attempts to cancel expired group purchases.");
    private readonly Gauge m_LastSuccess = Metrics.CreateGauge(
        "vmeste_expiration_last_success_timestamp_seconds", "Unix time of the last successful expiration check.");

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        do {
            try {
                await ReconcileGroupsAsync(cancellationToken);
                m_LastSuccess.Set(clock.GetUtcNow().ToUnixTimeSeconds());
            } catch (Exception exception) when (!cancellationToken.IsCancellationRequested) {
                m_Failures.Inc();
                logger.LogError(exception, "Failed to reconcile open group purchases");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task ReconcileGroupsAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var ids = await dbContext.GroupPurchases
            .Where(group => group.Status == EGroupPurchaseStatus.Open && (group.Deadline <= now ||
                (group.Participants.Any(p => p.UsesWallet && p.LimitDebitsKopecks == 0 && !p.LimitRevoked && p.Order == null && p.LimitKopecks != null) &&
                 group.PriceTiers.Any(tier => tier.RequiredParticipants <= group.Participants.Where(p => !p.LimitRevoked).Sum(p => p.Quantity)))))
            .OrderBy(group => group.Deadline).ThenBy(group => group.Id)
            .Select(group => group.Id).ToArrayAsync(cancellationToken);

        foreach (var id in ids) {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.LockGroupPurchaseAsync(id, cancellationToken);
            var group = await dbContext.GroupPurchases.Include(item => item.PriceTiers)
                .Include(item => item.Participants).ThenInclude(item => item.Order)
                .SingleAsync(item => item.Id == id, cancellationToken);
            if (group.Status == EGroupPurchaseStatus.Open) {
                await dbContext.LockUsersAsync(group.Participants.Select(p => p.UserId).ToArray(), cancellationToken);
                var balances = await dbContext.GetGroupWalletBalancesAsync([group], null, cancellationToken);
                group.UpdateOpenStatus(clock.GetUtcNow(), balances);
                if (group.Status != EGroupPurchaseStatus.Open) {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
