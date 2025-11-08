using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class SessionService(
    IDistributedCacheService cacheService,
    IOptions<JwtOptions> jwtOption) : ISessionService
{
    private readonly JwtOptions _jwtOption = jwtOption.Value;
    
    private static string RefreshTokenKey(Guid userId, string jti) => $"auth:refresh:{userId}:{jti}";
    private static string BlacklistKey(string jti) => $"auth:blacklist:{jti}";
    private static string TokenCooldownKey(string jti) => $"auth:rt_cooldown:{jti}";
    private static string UserSessionsKey(Guid userId) => $"auth:sessions_zset:{userId}";
    
    public async Task StoreRefreshTokenAsync(Guid userId, string jti, string refreshToken)
    {
        await cacheService.SetAsync(RefreshTokenKey(userId, jti), refreshToken, _jwtOption.RefreshTokenLifetime);
    }
    
    public async Task StartTokenCooldownAsync(string jti)
    {
        if (_jwtOption.TokenCooldownLifeTime <= TimeSpan.Zero) return;
        await cacheService.SetAsync(TokenCooldownKey(jti), "1", _jwtOption.TokenCooldownLifeTime);
    }
    
    public async Task<bool> IsTokenOnCooldownAsync(string jti)
    {
        return await cacheService.ExistsAsync(TokenCooldownKey(jti));
    }
    
    public async Task<bool> ConsumeRefreshTokenAsync(Guid userId, string jti, string refreshToken)
    {
        var key = RefreshTokenKey(userId, jti);
        return await cacheService.CompareAndRemoveAsync(key, refreshToken);
    }
    
    public async Task DeleteRefreshTokenAsync(Guid userId, string jti)
    {
        await cacheService.RemoveAsync(RefreshTokenKey(userId, jti));
    }
    
    private async Task BlacklistAccessTokenAsync(string jti)
    {
        await cacheService.SetAsync(BlacklistKey(jti), "revoked", _jwtOption.AccessTokenLifeTime);
    }

    public async Task BlacklistAccessTokenAsync(string jti, TimeSpan remainingTtl)
    {
        var finalTtl = remainingTtl > TimeSpan.FromSeconds(5)
            ? remainingTtl
            : TimeSpan.FromSeconds(5);
        
        await cacheService.SetAsync(BlacklistKey(jti), "revoked", finalTtl);
    }

    public async Task<bool> IsAccessTokenBlacklistedAsync(string jti)
    {
        return await cacheService.ExistsAsync(BlacklistKey(jti));
    }
    
    public async Task RegisterSessionAsync(Guid userId, string jti)
    {
        var refreshTokenExpiresAt = DateTime.UtcNow.Add(_jwtOption.RefreshTokenLifetime);
        var score = ((DateTimeOffset)refreshTokenExpiresAt).ToUnixTimeSeconds();
        await cacheService.SortedSetAddAsync(UserSessionsKey(userId), jti, score);
    }
    
    public async Task UnregisterSessionAsync(Guid userId, string jti)
    {
        await cacheService.SortedSetRemoveAsync(UserSessionsKey(userId), jti);
    }
    
    public async Task<long> GetActiveSessionCountAsync(Guid userId)
    {
        var key = UserSessionsKey(userId);
        var nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await cacheService.SortedSetRemoveRangeByScoreAsync(key, 0, nowTimestamp);
        
        return await cacheService.SortedSetLengthAsync(key);
    }

    public async Task BlacklistAllUserSessionsAsync(Guid userId)
    {
        var key = UserSessionsKey(userId);
        var nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var jtis = await cacheService.SortedSetRangeByScoreAsync(key, nowTimestamp);

        if (jtis.Length == 0) return;

        var tasks = new List<Task>();
        var refreshKeysToRemove = new List<string>();

        foreach (var jti in jtis)
        {
            tasks.Add(BlacklistAccessTokenAsync(jti)); 
            
            refreshKeysToRemove.Add(RefreshTokenKey(userId, jti));
        }

        if (refreshKeysToRemove.Count > 0)
        {
            tasks.Add(cacheService.RemoveAsync(refreshKeysToRemove));
        }

        tasks.Add(cacheService.RemoveAsync(key)); 

        await Task.WhenAll(tasks);
    }
}