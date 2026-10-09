using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Infrastructure.Outbox;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Outbox;

public class OutboxBackgroundServiceTests
{
    private readonly IOutboxSignal _signal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboxProcessor _processor;
    private readonly OutboxOptions _options;

    public OutboxBackgroundServiceTests()
    {
        _signal = Substitute.For<IOutboxSignal>();
        
        _processor = Substitute.For<IOutboxProcessor>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOutboxProcessor)).Returns(_processor);
        
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
        
        _options = new OutboxOptions { BatchSize = 10, PollingIntervalSeconds = 5 };
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProcessBatch_And_WaitForSignal_WhenBatchIsNotFull()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var capturedCts = cts;
        
        _processor.ProcessBatchAsync(Arg.Any<CancellationToken>()).Returns(5); // Less than BatchSize

        // Cancel after the first WaitForSignalAsync so the loop terminates
        _signal.WaitForSignalAsync(Arg.Any<CancellationToken>())
            .Returns(async _ => await capturedCts.CancelAsync());

        var service = new OutboxBackgroundService(_signal, _scopeFactory, Options.Create(_options), NullLogger<OutboxBackgroundService>.Instance);

        // Act
        await service.StartAsync(cts.Token);
        
        // Wait briefly for the background task to complete (or fail)
        await Task.Delay(100, CancellationToken.None);

        // Assert
        await _processor.Received(1).ProcessBatchAsync(Arg.Any<CancellationToken>());
        await _signal.Received(1).WaitForSignalAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProcessBatch_And_NotWaitForSignal_WhenBatchIsFull()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var capturedCts = cts;
        var callCount = 0;
        
        _processor.ProcessBatchAsync(Arg.Any<CancellationToken>())
            .Returns(async _ => 
            {
                callCount++;
                if (callCount >= 2) await capturedCts.CancelAsync(); // Terminate loop after second call
                return 10;
            });

        var service = new OutboxBackgroundService(_signal, _scopeFactory, Options.Create(_options), NullLogger<OutboxBackgroundService>.Instance);

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);

        // Assert
        // Should process batch at least twice, but never wait for signal because the batch was full
        await _processor.Received(2).ProcessBatchAsync(Arg.Any<CancellationToken>());
        await _signal.DidNotReceive().WaitForSignalAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_GracefullyStop_WhenCancellationRequestedDuringProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        
        _processor.ProcessBatchAsync(Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException(token)); // Simulate shutdown during processing

        var service = new OutboxBackgroundService(_signal, _scopeFactory, Options.Create(_options), NullLogger<OutboxBackgroundService>.Instance);

        // Terminate up-front to simulate stopping
        await cts.CancelAsync();

        // Act
        var act = async () => await service.StartAsync(token);

        // Assert
        await act.Should().NotThrowAsync();
        // Since we mocked the OCE precisely matching the token, the loop gracefully exits.
    }

    [Fact]
    public async Task ExecuteAsync_Should_CatchGenericException_And_DelayBeforeContinuing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var capturedCts = cts;
        var callCount = 0;
        
        // First call throws generic exception (e.g., DB error)
        // Second call processes 5 items and then cancels to stop the loop
        _processor.ProcessBatchAsync(Arg.Any<CancellationToken>())
            .Returns(_ => 
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("DB Timeout");
                return 5; 
            });

        _signal.WaitForSignalAsync(Arg.Any<CancellationToken>())
            .Returns(async _ => await capturedCts.CancelAsync());

        var service = new OutboxBackgroundService(_signal, _scopeFactory, Options.Create(_options), NullLogger<OutboxBackgroundService>.Instance);

        // Act
        // Because of the 5-second delay, we don't want to actually block the unit test for 5 seconds.
        // We will just let it start, wait a tiny bit to trigger the exception block.
        await service.StartAsync(cts.Token);
        
        await Task.Delay(50, CancellationToken.None);
        await cts.CancelAsync();
        
        await Task.Delay(200, CancellationToken.None);

        // Assert
        // The first call threw, it went into Task.Delay, got canceled, and exited gracefully.
        await _processor.Received(1).ProcessBatchAsync(Arg.Any<CancellationToken>());
        await _signal.DidNotReceive().WaitForSignalAsync(Arg.Any<CancellationToken>()); // Never reaches signal because loop exited in catch
    }
}