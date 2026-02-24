using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface ISessionService
{
    Task<Result> CreateLoginSessionAsync(Guid userId, string jti, string refreshToken);
    Task CreateRegisterSessionAsync(Guid userId, string jti, string refreshToken);
    Task<ConsumeResult> ConsumeRefreshTokenAsync(Guid userId, string jti, string refreshToken);
    Task RotateSessionAsync(Guid userId, string oldJti, string newJti, string newAccessToken, string newRefreshToken);
    Task RevokeSessionAsync(Guid userId, string jti, TimeSpan remainingTtl);
    Task RevokeAllSessionsAsync(Guid userId);
    Task<bool> IsAccessTokenBlacklistedAsync(string jti);
}

public readonly record struct ConsumeResult(
    bool IsSuccess,
    string? CachedAccessToken = null,
    string? CachedRefreshToken = null);