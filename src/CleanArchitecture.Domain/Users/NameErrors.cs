using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class NameErrors
{
    public static Error FirstNameCannotBeNullOrEmpty =>
        new("Name.FirstNameCannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error LastNameCannotBeNullOrEmpty =>
        new("Name.LastNameCannotBeNullOrEmpty", ErrorType.BadRequest);
    public static Error FirstNameLengthInvalid =>
        new("Name.FirstNameLengthInvalid", ErrorType.BadRequest);
    public static Error LastNameLengthInvalid =>
        new("Name.LastNameLengthInvalid", ErrorType.BadRequest);
}