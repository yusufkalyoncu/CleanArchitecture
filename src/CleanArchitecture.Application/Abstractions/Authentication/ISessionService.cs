namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface ISessionService
{
    Task StoreRefreshTokenAsync(Guid userId, string jti, string refreshToken);
    Task StartTokenCooldownAsync(string jti);
    Task<bool> IsTokenOnCooldownAsync(string jti);
    Task<bool> ConsumeRefreshTokenAsync(Guid userId, string jti, string refreshToken);
    Task DeleteRefreshTokenAsync(Guid userId, string jti);
    Task BlacklistAccessTokenAsync(string jti, TimeSpan remainingTtl);
    Task<bool> IsAccessTokenBlacklistedAsync(string jti);
    Task RegisterSessionAsync(Guid userId, string jti);
    Task UnregisterSessionAsync(Guid userId, string jti);
    Task<long> GetActiveSessionCountAsync(Guid userId);
    Task BlacklistAllUserSessionsAsync(Guid userId);
}