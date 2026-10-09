using CleanArchitecture.Application.Abstractions.Locking;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Locking;

public class DistributedLockManagerTests : BaseIntegrationTest
{
    private readonly IDistributedLockManager _lockManager;

    public DistributedLockManagerTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _lockManager = ServiceProvider.GetRequiredService<IDistributedLockManager>();
    }

    [Fact]
    public async Task TryExecuteWithLockAsync_Should_ExecuteAction_And_ReturnTrue_WhenLockIsAvailable()
    {
        // Arrange
        var resource = $"test-lock-{Guid.NewGuid()}";
        var actionExecuted = false;

        // Act
        var result = await _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(5),
            _ =>
            {
                actionExecuted = true;
                return Task.CompletedTask;
            });

        // Assert
        result.Should().BeTrue();
        actionExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task TryExecuteWithLockAsync_Should_ReturnFalse_And_NotExecuteAction_WhenAlreadyLocked()
    {
        // Arrange
        var resource = $"test-lock-concurrent-{Guid.NewGuid()}";
        var tcs = new TaskCompletionSource();
        var secondActionExecuted = false;

        // Act - Thread 1: Acquire lock and hold it indefinitely
        var thread1Task = _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(10), // hold for 10 seconds
            async _ =>
            {
                await tcs.Task; // Hold the lock until we signal tcs
            });

        // Wait a tiny bit to ensure Thread 1 acquires the lock
        await Task.Delay(100);

        // Act - Thread 2: Try to acquire the same lock with zero wait time
        var result = await _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero,
            TimeSpan.Zero,
            _ =>
            {
                secondActionExecuted = true;
                return Task.CompletedTask;
            });

        // Cleanup Thread 1
        tcs.SetResult();
        await thread1Task;

        // Assert
        result.Should().BeFalse();
        secondActionExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task TryExecuteWithLockAsync_Should_WaitAndAcquireLock_WhenRetryIsConfigured()
    {
        // Arrange
        var resource = $"test-lock-retry-{Guid.NewGuid()}";
        var secondActionExecuted = false;

        // Thread 1: Acquires lock for a short duration
        var thread1Task = _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(5), // TTL
            async cancellationToken =>
            {
                await Task.Delay(500, cancellationToken); // Hold for 500ms
            });

        await Task.Delay(50); // Ensure Thread 1 gets the lock first

        // Act - Thread 2: Try to acquire, should fail initially but retry and succeed
        var result = await _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(5), // TTL
            TimeSpan.FromSeconds(2), // Wait up to 2 seconds
            TimeSpan.FromMilliseconds(200), // Retry every 200ms
            _ =>
            {
                secondActionExecuted = true;
                return Task.CompletedTask;
            });

        await thread1Task;

        // Assert
        result.Should().BeTrue("Thread 2 should have waited and eventually acquired the lock.");
        secondActionExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task TryExecuteWithLockAsync_Should_ReleaseLock_WhenActionCompletes()
    {
        // Arrange
        var resource = $"test-lock-release-{Guid.NewGuid()}";

        // Act 1: Acquire and complete
        var firstResult = await _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(10),
            _ => Task.CompletedTask);

        // Act 2: Acquire again immediately
        var secondResult = await _lockManager.TryExecuteWithLockAsync(
            resource,
            TimeSpan.FromSeconds(10),
            TimeSpan.Zero,
            TimeSpan.Zero,
            _ => Task.CompletedTask);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeTrue("The first call should have released the lock, allowing the second call to succeed immediately.");
    }
}