using System.ComponentModel.DataAnnotations;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public readonly record struct Email
{
    public const int MaxLength = 100;
    
    public string Value { get; }
    
    private Email(string value) => Value = value;
    
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(EmailErrors.CannotBeNullOrEmpty);
        }
        
        if (value.Length > MaxLength)
        {
            return Result.Failure<Email>(EmailErrors.LengthExceeded);
        }

        if (!new EmailAddressAttribute().IsValid(value))
        {
            return Result.Failure<Email>(EmailErrors.InvalidFormat);
        }
        
        return new Email(value);
    }
    
    public static Email FromValue(string value)
    {
        return new Email(value);
    }
}