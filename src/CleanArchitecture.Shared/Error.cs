namespace CleanArchitecture.Shared;

public record Error
{
    #region Properties

    public string ErrorCode { get; }
    public ErrorType Type { get; }
    public Dictionary<string, object?>? Args { get; private init; }

    #endregion

    #region Constructor

    internal Error(string errorCode, ErrorType type)
    {
        ErrorCode = errorCode;
        Type = type;
    }

    #endregion

    #region Equality

    public virtual bool Equals(Error? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ErrorCode == other.ErrorCode && Type == other.Type;
    }

    public override int GetHashCode() => HashCode.Combine(ErrorCode, Type);

    #endregion

    #region Factory Methods

    private static Error Create(string errorCode, ErrorType type, Dictionary<string, object?>? args) =>
        new(errorCode, type) { Args = args };

    public static readonly Error None = new(string.Empty, ErrorType.None);
    public static readonly Error NullValue = new("General.Null", ErrorType.BadRequest);
    public static readonly Error Unexpected = new("General.Unexpected", ErrorType.InternalServerError);

    public static Error Validation(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.BadRequest, args);
    
    public static Error BadRequest(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.BadRequest, args);

    public static Error NotFound(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.NotFound, args);

    public static Error Unauthorized(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.Unauthorized, args);

    public static Error Forbidden(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.Forbidden, args);

    public static Error Conflict(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.Conflict, args);

    public static Error TooManyRequests(string errorCode, Dictionary<string, object?>? args = null) =>
        Create(errorCode, ErrorType.TooManyRequests, args);

    #endregion
}

public sealed record ValidationError(Error[] Errors) : Error("General.Validation", ErrorType.BadRequest);