using CleanArchitecture.Application.Abstractions.Locking;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Locking;

public static class LockExtensions
{
    public static IServiceCollection AddLockManager(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLockManager, RedisLockManager>();

        return services;
    }
}