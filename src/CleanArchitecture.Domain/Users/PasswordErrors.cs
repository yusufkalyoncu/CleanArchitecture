using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class PasswordErrors
{
    public static Error PasswordCannotBeNullOrEmpty =>
        new("Password.CannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error PasswordLengthInvalid =>
        new("Password.LengthInvalid", ErrorType.BadRequest);
}