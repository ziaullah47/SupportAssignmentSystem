using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SupportAssignmentSystem.Core.Configuration;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Infrastructure.Data;
using SupportAssignmentSystem.Infrastructure.Storage;

namespace SupportAssignmentSystem.Infrastructure.Extensions;

public static class StorageServiceExtensions
{
    public static async Task<IServiceCollection> AddStorageServicesAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var storageConfig = new StorageConfiguration();
        configuration.GetSection("Storage").Bind(storageConfig);
        services.AddSingleton(storageConfig);

        switch (storageConfig.StorageType)
        {
            case StorageType.InMemory:
                services.AddSingleton<ISessionStorage, InMemorySessionStorage>();
                break;

            case StorageType.Redis:
                // Configure Redis asynchronously
                var redisConnection = await ConnectionMultiplexer.ConnectAsync(
                    storageConfig.RedisConfiguration.ConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(redisConnection);
                services.AddSingleton<ISessionStorage>(sp =>
                {
                    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                    return new RedisSessionStorage(
                        redis,
                        storageConfig.RedisConfiguration.InstanceName);
                });
                break;

            case StorageType.Database:
                // Configure Database
                services.AddDbContextFactory<SupportAssignmentDbContext>(options =>
                {
                    var connectionString = storageConfig.DatabaseConfiguration.ConnectionString;
                    var provider = storageConfig.DatabaseConfiguration.Provider;

                    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                    {
                        options.UseNpgsql(connectionString);
                    }
                    else // Default to SQL Server
                    {
                        options.UseSqlServer(connectionString);
                    }
                });
                services.AddSingleton<ISessionStorage, DatabaseSessionStorage>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported storage type: {storageConfig.StorageType}");
        }

        return services;
    }

    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddStorageServicesAsync(services, configuration).GetAwaiter().GetResult();
    }

    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        var storageConfig = serviceProvider.GetRequiredService<StorageConfiguration>();

        if (storageConfig.StorageType == StorageType.Database)
        {
            var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<SupportAssignmentDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }
}

