using CleanArchitecture.Application.Abstractions.Caching;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Caching;

public class CacheServiceTests : BaseIntegrationTest
{
    private readonly ICacheService _cacheService;

    public CacheServiceTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _cacheService = ServiceProvider.GetRequiredService<ICacheService>();
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_Should_StoreAndRetrieveValue()
    {
        // Arrange
        var key = "test_key";
        var expectedValue = "test_value";

        // Act
        await _cacheService.SetAsync(key, expectedValue, TimeSpan.FromMinutes(5));
        var retrievedValue = await _cacheService.GetAsync<string>(key);

        // Assert
        retrievedValue.Should().Be(expectedValue);
    }

    [Fact]
    public async Task GetAsync_Should_ReturnDefault_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "non_existent_key";

        // Act
        var retrievedValue = await _cacheService.GetAsync<string>(key);

        // Assert
        retrievedValue.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_Should_DeleteKey()
    {
        // Arrange
        var key = "delete_key";
        await _cacheService.SetAsync(key, "value");

        // Act
        await _cacheService.RemoveAsync(key);
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_MultipleKeys_Should_DeleteAllProvidedKeys()
    {
        // Arrange
        var keys = new[] { "key1", "key2", "key3" };
        foreach (var key in keys)
        {
            await _cacheService.SetAsync(key, "value");
        }

        // Act
        await _cacheService.RemoveAsync(keys);

        // Assert
        foreach (var key in keys)
        {
            var exists = await _cacheService.ExistsAsync(key);
            exists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ExistsAsync_Should_ReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = "exists_key";
        await _cacheService.SetAsync(key, "value");

        // Act
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrSetAsync_Should_ExecuteFactory_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "factory_key";
        var factoryExecuted = false;

        Task<string> Factory()
        {
            factoryExecuted = true;
            return Task.FromResult("factory_value");
        }

        // Act
        var result = await _cacheService.GetOrSetAsync(key, Factory);

        // Assert
        result.Should().Be("factory_value");
        factoryExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrSetAsync_Should_ReturnCachedValue_And_NotExecuteFactory_WhenKeyExists()
    {
        // Arrange
        var key = "cached_factory_key";
        await _cacheService.SetAsync(key, "existing_value");
        var factoryExecuted = false;

        Task<string> Factory()
        {
            factoryExecuted = true;
            return Task.FromResult("factory_value");
        }

        // Act
        var result = await _cacheService.GetOrSetAsync(key, Factory);

        // Assert
        result.Should().Be("existing_value");
        factoryExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_Should_RemoveAllKeys_ByTriggeringCancellationToken()
    {
        // Arrange
        await _cacheService.SetAsync("key1", "value1");
        await _cacheService.SetAsync("key2", "value2");
        await _cacheService.GetOrSetAsync("key3", () => Task.FromResult("value3"));

        // Verify they exist first
        (await _cacheService.ExistsAsync("key1")).Should().BeTrue();
        (await _cacheService.ExistsAsync("key2")).Should().BeTrue();
        (await _cacheService.ExistsAsync("key3")).Should().BeTrue();

        // Act
        await _cacheService.ClearAsync();

        // Let the cancellation token propagate in the background
        await Task.Delay(50); 

        // Assert
        (await _cacheService.ExistsAsync("key1")).Should().BeFalse();
        (await _cacheService.ExistsAsync("key2")).Should().BeFalse();
        (await _cacheService.ExistsAsync("key3")).Should().BeFalse();
    }
}