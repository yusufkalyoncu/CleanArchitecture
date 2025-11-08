using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CleanArchitecture.Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal? principal)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : throw new ApplicationException("User id is unavailable");
    }
    
    public static string GetJti(this ClaimsPrincipal? principal)
    {
        var jti = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        
        return !string.IsNullOrWhiteSpace(jti)
            ? jti
            : throw new ApplicationException("Jti is unavailable");
    }
    
    private static DateTime? GetAccessTokenExpiry(this ClaimsPrincipal user)
    {
        var expClaim = user.FindFirst(JwtRegisteredClaimNames.Exp);
        if (expClaim == null)
            return null;

        if (!long.TryParse(expClaim.Value, out var expUnix))
            return null;

        var expiryUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

        return expiryUtc > DateTime.UtcNow ? expiryUtc : null;
    }
    
    public static TimeSpan GetAccessTokenRemainingLifetime(this ClaimsPrincipal user)
    {
        var expiry = user.GetAccessTokenExpiry();
        if (expiry == null)
            return TimeSpan.Zero;

        var remaining = expiry.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}