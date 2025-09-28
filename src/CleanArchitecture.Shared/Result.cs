namespace CleanArchitecture.Shared;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new InvalidOperationException();
        }
        
        IsSuccess = isSuccess;
        Error = error;
    }
    
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    
    public static Result Success() => new(true, Error.None);
    public static Result<TValue> Success<TValue>(TValue value) => new(true, Error.None, value);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Failure<TValue>(Error error) => new(false, error);
}

public class Result<T>(bool isSuccess, Error error, T? data = default) : Result(isSuccess, error)
{
    public T Data => IsSuccess
        ? data!
        : throw new InvalidOperationException("The data of a failure result can't be accessed.");
    
    public static implicit operator Result<T>(T? value) =>
        value is not null ? Success(value) : Failure<T>(Error.NullValue);
}