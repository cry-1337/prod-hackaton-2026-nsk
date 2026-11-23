using Backend.Application.Common.Interfaces;
using Backend.Infrastructure.Monitoring;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? postgresConnectionString,
        string? redisConnectionString
    ) {
        services.AddDbContext<IAppDbContext, AppDbContext>(options =>
            options.UseNpgsql(
                postgresConnectionString ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING_MISSING"),
                postgres => postgres.SetPostgresVersion(17, 0)));
        services.AddHostedService<GroupPurchaseExpirationService>();
        services.AddHostedService<GroupPurchaseMetricsService>();
        services.AddHostedService<PostgresResourceMetricsService>();

        services.AddSingleton<IUserPasswords, BCryptPasswords>();

        services.AddStackExchangeRedisCache(options => {
            options.Configuration = redisConnectionString ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING_MISSING");
            options.InstanceName = "backend:";
        });

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken) {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
