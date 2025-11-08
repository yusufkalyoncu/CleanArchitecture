using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class PasswordErrors
{
    public static Error CannotBeNullOrEmpty =>
        new("Password.CannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error InvalidLength =>
        new("Password.LengthInvalid", ErrorType.BadRequest);
}