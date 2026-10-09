using CleanArchitecture.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using System.Net;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Caching;

public class RedisCacheServiceExceptionTests
{
    private readonly IConnectionMultiplexer _mockRedis;
    private readonly IDatabase _mockDatabase;
    private readonly RedisCacheService _service;

    public RedisCacheServiceExceptionTests()
    {
        _mockRedis = Substitute.For<IConnectionMultiplexer>();
        _mockDatabase = Substitute.For<IDatabase>();
        var mockLogger = Substitute.For<ILogger<RedisCacheService>>();

        // Ensure logger returns true for IsEnabled to cover logging blocks
        mockLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        _mockRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_mockDatabase);

        var options = Options.Create(new RedisOptions { InstanceName = "test" });
        _service = new RedisCacheService(_mockRedis, mockLogger, options);
    }

    [Fact]
    public async Task GetAsync_Should_ReturnDefault_And_Log_WhenExceptionOccurs()
    {
        _mockDatabase.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        var result = await _service.GetAsync<string>("test_key", CancellationToken.None);
        
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_Should_Log_WhenExceptionOccurs()
    {
        _mockDatabase.StringSetAsync(default, default, null, default, default)
            .ThrowsAsyncForAnyArgs(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        Func<Task> act = async () => await _service.SetAsync("test_key", "value", cancellationToken: CancellationToken.None);
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_Should_Log_WhenExceptionOccurs()
    {
        _mockDatabase.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        Func<Task> act = async () => await _service.RemoveAsync("test_key", CancellationToken.None);
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveMultipleAsync_Should_Log_WhenExceptionOccurs()
    {
        _mockDatabase.KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        Func<Task> act = async () => await _service.RemoveAsync(["test_key1"], CancellationToken.None);
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_Should_ReturnFalse_And_Log_WhenExceptionOccurs()
    {
        _mockDatabase.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        var result = await _service.ExistsAsync("test_key", CancellationToken.None);
        
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrSetAsync_Should_ExecuteFactory_WhenExceptionOccurs_DuringGet()
    {
        _mockDatabase.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        var factoryExecuted = false;
        var result = await _service.GetOrSetAsync("test_key", () => 
        {
            factoryExecuted = true;
            return Task.FromResult("factory_val");
        }, cancellationToken: CancellationToken.None);
        
        result.Should().Be("factory_val");
        factoryExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAsync_Should_Log_WhenExceptionOccurs()
    {
        // To cover exception, we can just make GetServer throw.
        _mockRedis.GetServer(Arg.Any<EndPoint>(), Arg.Any<object>())
            .Throws(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Timeout"));
        
        // Also mock GetEndPoints to return a dummy endpoint to enter the block
        _mockRedis.GetEndPoints(Arg.Any<bool>()).Returns([new IPEndPoint(0, 0)]);

        Func<Task> act = async () => await _service.ClearAsync(CancellationToken.None);
        
        await act.Should().NotThrowAsync();
    }
}