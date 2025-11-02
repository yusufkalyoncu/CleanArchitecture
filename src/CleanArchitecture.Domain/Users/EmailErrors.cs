using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class EmailErrors
{
    public static Error EmailCannotBeNullOrEmpty =>
        new("Email.CannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error EmailIsInvalid =>
        new("Email.IsInvalid", ErrorType.BadRequest);
}