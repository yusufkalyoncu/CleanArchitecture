using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class EmailErrors
{
    public static Error CannotBeNullOrEmpty =>
        new("Email.CannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error InvalidFormat =>
        new("Email.IsInvalid", ErrorType.BadRequest);
    public static Error LengthExceeded =>
        new("Email.LengthExceeded", ErrorType.BadRequest);
}