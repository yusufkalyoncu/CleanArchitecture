using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Caching;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Shared;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class SessionService(
    IDistributedCacheService cacheService,
    IOptions<JwtOptions> jwtOption) : ISessionService
{
    private readonly JwtOptions _jwtOption = jwtOption.Value;
    private const string BlacklistValue = "revoked";
    private static readonly string SerializedBlacklistValue = $"\"{BlacklistValue}\"";
    private const int MaxSessions = 5;

    #region Login & Register

    public async Task<Result> CreateLoginSessionAsync(Guid userId, string jti, string refreshToken)
    {
        var activeCount = await GetActiveSessionCountInternalAsync(userId);
        if (activeCount >= MaxSessions) return Result.Failure(UserErrors.MaxSessionsReached);

        var batch = cacheService.CreateBatch();
        batch.SetAsync(RefreshTokenKey(userId, jti), refreshToken, _jwtOption.RefreshTokenLifetime);
        batch.SortedSetAddAsync(UserSessionsKey(userId), jti, GetExpiryScore());
        await batch.ExecuteAsync();

        return Result.Success();
    }

    public async Task CreateRegisterSessionAsync(Guid userId, string jti, string refreshToken)
    {
        var batch = cacheService.CreateBatch();
        batch.SetAsync(RefreshTokenKey(userId, jti), refreshToken, _jwtOption.RefreshTokenLifetime);
        batch.SortedSetAddAsync(UserSessionsKey(userId), jti, GetExpiryScore());
        await batch.ExecuteAsync();
    }

    #endregion

    #region Refresh (Consume & Rotate)

    public async Task<ConsumeResult> ConsumeRefreshTokenAsync(Guid userId, string jti, string refreshToken)
    {
        var cached = await cacheService.GetAsync<ConsumeResult?>(GracePeriodKey(jti));
        if (cached != null) return cached.Value;

        var removed = await cacheService.CompareAndRemoveAsync(RefreshTokenKey(userId, jti), refreshToken);
        return new ConsumeResult(removed);
    }

    public async Task RotateSessionAsync(Guid userId, string oldJti, string newJti, string newAt, string newRt)
    {
        var batch = cacheService.CreateBatch();
        var score = GetExpiryScore();

        batch.RemoveAsync(RefreshTokenKey(userId, oldJti));
        batch.SortedSetRemoveAsync(UserSessionsKey(userId), oldJti);

        batch.SetAsync(RefreshTokenKey(userId, newJti), newRt, _jwtOption.RefreshTokenLifetime);
        batch.SortedSetAddAsync(UserSessionsKey(userId), newJti, score);

        batch.SetAsync(GracePeriodKey(oldJti), new ConsumeResult(
            true,
            newAt,
            newRt), _jwtOption.GracePeriodLifeTime);

        await batch.ExecuteAsync();
    }

    #endregion

    #region Logout & Blacklist

    public async Task RevokeSessionAsync(Guid userId, string jti, TimeSpan remainingTtl)
    {
        var batch = cacheService.CreateBatch();
        
        if (remainingTtl > TimeSpan.FromSeconds(5))
            batch.SetAsync(BlacklistKey(jti), BlacklistValue, remainingTtl);

        batch.RemoveAsync(RefreshTokenKey(userId, jti));
        batch.SortedSetRemoveAsync(UserSessionsKey(userId), jti);

        await batch.ExecuteAsync();
    }

    public async Task RevokeAllSessionsAsync(Guid userId)
    {
        const string script = @"
            local jtis = redis.call('ZRANGEBYSCORE', KEYS[1], ARGV[1], '+inf')
            for _, jti in ipairs(jtis) do
                redis.call('SET', KEYS[3] .. jti, ARGV[3], 'EX', ARGV[2])
                redis.call('DEL', KEYS[2] .. jti)
            end
            return redis.call('DEL', KEYS[1])";

        var keys = new[] 
        { 
            UserSessionsKey(userId), 
            $"auth:rt:{userId}:", 
            "auth:bl:" 
        };

        var args = new object[]
        {
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            (int)_jwtOption.AccessTokenLifeTime.TotalSeconds,
            SerializedBlacklistValue
        };

        await cacheService.ExecuteScriptAsync<long>(script, keys, args);
    }

    public async Task<bool> IsAccessTokenBlacklistedAsync(string jti) => 
        await cacheService.ExistsAsync(BlacklistKey(jti));

    #endregion

    #region Helpers

    private static string RefreshTokenKey(Guid u, string j) => $"auth:rt:{u}:{j}";
    private static string BlacklistKey(string j) => $"auth:bl:{j}";
    private static string GracePeriodKey(string j) => $"auth:grace:{j}";
    private static string UserSessionsKey(Guid u) => $"auth:sessions:{u}";

    private double GetExpiryScore() => ((DateTimeOffset)DateTime.UtcNow.Add(_jwtOption.RefreshTokenLifetime)).ToUnixTimeSeconds();

    private async Task<long> GetActiveSessionCountInternalAsync(Guid userId)
    {
        const string script = @"
            redis.call('ZREMRANGEBYSCORE', KEYS[1], ARGV[1], ARGV[2])
            return redis.call('ZCARD', KEYS[1])";

        var keys = new[] { UserSessionsKey(userId) };
        var args = new object[] { 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

        return await cacheService.ExecuteScriptAsync<long>(script, keys, args);
    }

    #endregion
}