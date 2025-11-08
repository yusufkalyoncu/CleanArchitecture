using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class UserErrors
{
    public static Error InvalidCredentials
        => new("User.InvalidCredentials", ErrorType.Unauthorized);
    public static Error MaxSessionsReached
        => new("User.MaxSessionsReached", ErrorType.TooManyRequests);
    public static Error TokenOnCooldown
        => new("User.TokenOnCooldown", ErrorType.TooManyRequests);
    public static Error AlreadyExists
        => new("User.AlreadyExists", ErrorType.Conflict);
    public static Error InvalidToken
        => new("User.InvalidToken", ErrorType.Unauthorized);
}