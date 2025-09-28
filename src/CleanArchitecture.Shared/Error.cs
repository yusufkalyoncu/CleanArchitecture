namespace CleanArchitecture.Shared;

public record Error(string ErrorCode, ErrorType Type)
{
    public static Error None => new(string.Empty, ErrorType.None);
    public static Error NullValue => new("General.Null", ErrorType.BadRequest);
    public static Error Validation(string errorCode) => new(errorCode, ErrorType.BadRequest);
}

public record ValidationError(Error[] Errors) : Error("General.Validation", ErrorType.BadRequest);