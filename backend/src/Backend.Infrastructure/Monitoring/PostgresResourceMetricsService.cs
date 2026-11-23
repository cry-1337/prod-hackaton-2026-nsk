using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Backend.Infrastructure.Monitoring;

internal sealed class PostgresResourceMetricsService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<PostgresResourceMetricsService> logger
) : BackgroundService {
    private readonly Gauge m_Connections = Metrics.CreateGauge(
        "vmeste_postgres_client_connections", "Visible client connections across the PostgreSQL instance.");
    private readonly Gauge m_ConnectionLimit = Metrics.CreateGauge(
        "vmeste_postgres_connection_limit", "PostgreSQL max_connections on this instance.");
    private readonly Gauge m_LockWaiters = Metrics.CreateGauge(
        "vmeste_postgres_lock_waiting_sessions", "Sessions in the application database waiting for a heavyweight lock.");
    private readonly Gauge m_Deadlocks = Metrics.CreateGauge(
        "vmeste_postgres_deadlocks_since_reset", "Deadlocks detected in the application database since PostgreSQL statistics were reset.");
    private readonly Gauge m_Up = Metrics.CreateGauge(
        "vmeste_postgres_stats_up", "Whether the most recent PostgreSQL resource statistics query succeeded.");

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        do {
            try {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var snapshot = await db.Database.SqlQuery<Snapshot>($"""
                    SELECT (SELECT count(*) FROM pg_stat_activity WHERE backend_type = 'client backend') AS "Connections",
                           current_setting('max_connections')::integer AS "ConnectionLimit",
                           (SELECT count(DISTINCT locks.pid)
                              FROM pg_locks AS locks
                              JOIN pg_stat_activity AS activity ON activity.pid = locks.pid
                             WHERE NOT locks.granted AND activity.datname = current_database()) AS "LockWaiters",
                           deadlocks AS "Deadlocks"
                      FROM pg_stat_database
                     WHERE datname = current_database()
                    """).SingleAsync(cancellationToken);

                m_Connections.Set(snapshot.Connections);
                m_ConnectionLimit.Set(snapshot.ConnectionLimit);
                m_LockWaiters.Set(snapshot.LockWaiters);
                m_Deadlocks.Set(snapshot.Deadlocks);
                m_Up.Set(1);
            } catch (Exception exception) when (!cancellationToken.IsCancellationRequested) {
                m_Up.Set(0);
                logger.LogWarning(exception, "Failed to read PostgreSQL resource statistics");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private sealed record Snapshot(long Connections, int ConnectionLimit, long LockWaiters, long Deadlocks);
}
