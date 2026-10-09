using CleanArchitecture.Application.Abstractions.Caching;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Caching;

public class DistributedCacheServiceTests : BaseIntegrationTest
{
    private readonly IDistributedCacheService _cacheService;

    public DistributedCacheServiceTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _cacheService = ServiceProvider.GetRequiredService<IDistributedCacheService>();
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_Should_StoreAndRetrieveValue()
    {
        // Arrange
        var key = $"test_key_{Guid.NewGuid()}";
        var expectedValue = "test_value_distributed";

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
        var key = $"non_existent_key_{Guid.NewGuid()}";

        // Act
        var retrievedValue = await _cacheService.GetAsync<string>(key);

        // Assert
        retrievedValue.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_Should_DeleteKey()
    {
        // Arrange
        var key = $"delete_key_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "value");

        // Act
        await _cacheService.RemoveAsync(key);
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrSetAsync_Should_ExecuteFactory_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = $"factory_key_{Guid.NewGuid()}";
        var factoryExecuted = false;

        Task<string> Factory()
        {
            factoryExecuted = true;
            return Task.FromResult("distributed_factory_value");
        }

        // Act
        var result = await _cacheService.GetOrSetAsync(key, Factory);

        // Assert
        result.Should().Be("distributed_factory_value");
        factoryExecuted.Should().BeTrue();
    }
    
    [Fact]
    public async Task SetAddAsync_And_GetSetMembersAsync_Should_ManageUnorderedSet()
    {
        // Arrange
        var key = $"unordered_set_{Guid.NewGuid()}";
        
        // Act
        await _cacheService.SetAddAsync(key, "member1");
        await _cacheService.SetAddAsync(key, "member2");
        await _cacheService.SetAddAsync(key, "member2"); // duplicate should be ignored by distributed set
        
        var members = await _cacheService.GetSetMembersAsync(key);
        var length = await _cacheService.GetSetLengthAsync(key);
        
        await _cacheService.SetRemoveAsync(key, "member1");
        var membersAfterRemove = await _cacheService.GetSetMembersAsync(key);

        // Assert
        length.Should().Be(2);
        members.Should().BeEquivalentTo("member1", "member2");
        membersAfterRemove.Should().BeEquivalentTo("member2");
    }

    [Fact]
    public async Task OrderedSetAddAsync_And_GetOrderedSetRangeByScoreAsync_Should_ManageOrderedSet()
    {
        // Arrange
        var key = $"ordered_set_{Guid.NewGuid()}";
        
        // Act
        await _cacheService.OrderedSetAddAsync(key, "low_priority", 10.0);
        await _cacheService.OrderedSetAddAsync(key, "high_priority", 99.9);
        await _cacheService.OrderedSetAddAsync(key, "medium_priority", 50.0);
        
        var length = await _cacheService.GetOrderedSetLengthAsync(key);
        var allMembers = await _cacheService.GetOrderedSetRangeByScoreAsync(key);
        var topMembers = await _cacheService.GetOrderedSetRangeByScoreAsync(key, 50.0, 100.0);
        
        await _cacheService.RemoveOrderedSetRangeByScoreAsync(key, 0, 20.0); // Removes low_priority
        var remainingLength = await _cacheService.GetOrderedSetLengthAsync(key);

        // Assert
        length.Should().Be(3);
        // Scores are sorted ascending: 10, 50, 99.9
        allMembers.Should().ContainInOrder("low_priority", "medium_priority", "high_priority");
        topMembers.Should().ContainInOrder("medium_priority", "high_priority");
        remainingLength.Should().Be(2);
    }

[Fact]
    public async Task RemoveMultipleAsync_Should_DeleteAllKeys()
    {
        var key1 = $"multi1_{Guid.NewGuid()}";
        var key2 = $"multi2_{Guid.NewGuid()}";

        await _cacheService.SetAsync(key1, "val1");
        await _cacheService.SetAsync(key2, "val2");

        await _cacheService.RemoveAsync([key1, key2]);

        var exists1 = await _cacheService.ExistsAsync(key1);
        var exists2 = await _cacheService.ExistsAsync(key2);

        exists1.Should().BeFalse();
        exists2.Should().BeFalse();
    }
    
    [Fact]
    public async Task RemoveMultipleAsync_EmptyList_Should_NotThrow()
    {
        Func<Task> act = async () => await _cacheService.RemoveAsync(Array.Empty<string>());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ClearAsync_Should_ClearAllKeysMatchingInstanceName()
    {
        var key = $"clear_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "val");

        await _cacheService.ClearAsync(CancellationToken.None);

        var exists = await _cacheService.ExistsAsync(key);
        exists.Should().BeFalse();
    }
    
    [Fact]
    public async Task ClearAsync_Should_Log_WhenNoKeysFound()
    {
        // Clear all keys first to ensure it's empty
        await _cacheService.ClearAsync(CancellationToken.None);
        
        // Clear again, this time no keys will be found
        var act = async () => await _cacheService.ClearAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
    
    [Fact]
    public async Task ClearAsync_Should_Break_WhenCancelled()
    {
        var key = $"clear_cancel_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "val", cancellationToken: CancellationToken.None);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately before calling
        
        var token = cts.Token;
        Func<Task> act = async () => await _cacheService.ClearAsync(token);
        await act.Should().NotThrowAsync();
        
        // Since it broke early, the key should still exist
        var exists = await _cacheService.ExistsAsync(key, CancellationToken.None);
        exists.Should().BeTrue();
    }
    
    [Fact]
    public async Task CompareAndRemoveAsync_Should_Remove_WhenValueMatches()
    {
        var key = $"cas_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "match_val");

        var result = await _cacheService.CompareAndRemoveAsync(key, "match_val");
        result.Should().BeTrue();

        var exists = await _cacheService.ExistsAsync(key);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CompareAndRemoveAsync_Should_NotRemove_WhenValueMismatches()
    {
        var key = $"cas2_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "match_val");

        var result = await _cacheService.CompareAndRemoveAsync(key, "wrong_val");
        result.Should().BeFalse();

        var exists = await _cacheService.ExistsAsync(key);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteScriptAsync_Should_ExecuteLuaScript()
    {
        var key = $"script_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, 42); // stored as "42"

        // simple script: multiply value by 2
        var script = @"
            local val = redis.call('get', KEYS[1])
            if val then
                return tonumber(val) * tonumber(ARGV[1])
            else
                return 0
            end";

        var result = await _cacheService.ExecuteScriptAsync<long>(script, [key], [2]);
        
        result.Should().Be(84);
    }
    
    [Fact]
    public async Task ExecuteScriptAsync_Should_ReturnDefault_WhenScriptReturnsNull()
    {
        var key = $"script_null_{Guid.NewGuid()}";

        // script that just returns nil
        var script = @"return nil";

        var result = await _cacheService.ExecuteScriptAsync<string>(script, [key], Array.Empty<object>());
        
        result.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_Should_ReturnBatchInstance()
    {
        var batch = _cacheService.CreateBatch();
        batch.Should().NotBeNull();
    }
    
    [Fact]
    public async Task GetOrSetAsync_Should_ReturnCachedValue_WhenKeyExists()
    {
        var key = $"getorset_hit_{Guid.NewGuid()}";
        await _cacheService.SetAsync(key, "cached_val");

        var factoryExecuted = false;
        var result = await _cacheService.GetOrSetAsync(key, () =>
        {
            factoryExecuted = true;
            return Task.FromResult("factory_val");
        });

        result.Should().Be("cached_val");
        factoryExecuted.Should().BeFalse();
    }
    
    [Fact]
    public async Task RedisCacheBatch_Should_QueueAndExecuteCommands()
    {
        var batch = _cacheService.CreateBatch();
        
        var key1 = $"batch1_{Guid.NewGuid()}";
        var key2 = $"batch2_{Guid.NewGuid()}";
        var key3 = $"batch3_{Guid.NewGuid()}";

        // Setup some initial state
        await _cacheService.SetAsync(key2, "to_delete");
        await _cacheService.OrderedSetAddAsync(key3, "old_member", 1.0);
        
        // Use batch to mutate
        batch.SetAsync(key1, "val1", TimeSpan.FromMinutes(1));
        batch.RemoveAsync(key2);
        batch.SortedSetAddAsync(key3, "new_member", 2.0);
        batch.SortedSetRemoveAsync(key3, "old_member");
        
        // Execute batch
        await batch.ExecuteAsync();
        
        // Assert results
        var val1 = await _cacheService.GetAsync<string>(key1);
        val1.Should().Be("val1");
        
        var exists2 = await _cacheService.ExistsAsync(key2);
        exists2.Should().BeFalse();
        
        var members = await _cacheService.GetOrderedSetRangeByScoreAsync(key3);
        members.Should().ContainSingle().Which.Should().Be("new_member");
    }

    [Fact]
    public async Task SetRemoveAsync_Should_RemoveMemberFromUnorderedSet()
    {
        var key = $"unordered_remove_{Guid.NewGuid()}";
        
        await _cacheService.SetAddAsync(key, "mem1");
        await _cacheService.SetAddAsync(key, "mem2");
        
        await _cacheService.SetRemoveAsync(key, "mem1");
        
        var members = await _cacheService.GetSetMembersAsync(key);
        members.Should().ContainSingle().Which.Should().Be("mem2");
    }

    [Fact]
    public async Task OrderedSetRemoveAsync_Should_RemoveMemberFromOrderedSet()
    {
        var key = $"ordered_remove_{Guid.NewGuid()}";
        
        await _cacheService.OrderedSetAddAsync(key, "mem1", 10.0);
        await _cacheService.OrderedSetAddAsync(key, "mem2", 20.0);
        
        var removed = await _cacheService.OrderedSetRemoveAsync(key, "mem1");
        removed.Should().BeTrue();
        
        var length = await _cacheService.GetOrderedSetLengthAsync(key);
        length.Should().Be(1);
    }
}