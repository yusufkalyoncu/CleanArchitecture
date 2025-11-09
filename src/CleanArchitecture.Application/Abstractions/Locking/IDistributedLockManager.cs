namespace CleanArchitecture.Application.Abstractions.Locking;

public interface IDistributedLockManager
{
    Task<bool> TryExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
    
    Task<bool> TryExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan waitTime,
        TimeSpan retryTime,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}