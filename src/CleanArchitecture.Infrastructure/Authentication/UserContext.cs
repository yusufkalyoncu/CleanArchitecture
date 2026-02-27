using System.Security.Claims;
using CleanArchitecture.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    #region Backing Fields

    private Guid? _id;
    private string? _jti;
    private DateTime? _expireAt;
    private string? _ipAddress;
    private string? _userAgent;

    #endregion

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid Id => _id ??= GetUserId();

    public string Jti => _jti ??= HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Jti) ?? string.Empty;

    private DateTime? AccessTokenExpireAt => _expireAt ??= GetExpireAt();

    public TimeSpan AccessTokenRemainingLifetime
    {
        get
        {
            var expiry = AccessTokenExpireAt;
            if (expiry == null) return TimeSpan.Zero;

            var remaining = expiry.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public string? IpAddress => _ipAddress ??= HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _userAgent ??= HttpContext?.Request.Headers.UserAgent.ToString();

    #region Helper Methods

    private Guid GetUserId()
    {
        var userId = HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : Guid.Empty;
    }

    private DateTime? GetExpireAt()
    {
        var expClaim = HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (string.IsNullOrEmpty(expClaim) || !long.TryParse(expClaim, out var expUnix))
            return null;

        return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
    }

    #endregion
}