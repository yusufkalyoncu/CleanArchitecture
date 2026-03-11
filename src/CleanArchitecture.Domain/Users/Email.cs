using System.ComponentModel.DataAnnotations;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public readonly record struct Email
{
    public const int MaxLength = 100;
    
    public string Value { get; }

    public override string ToString() => Value;

    private Email(string value) => Value = value;
    
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(UserErrors.Email.Empty);
        }
        
        if (value.Length > MaxLength)
        {
            return Result.Failure<Email>(UserErrors.Email.TooLong);
        }

        if (!new EmailAddressAttribute().IsValid(value))
        {
            return Result.Failure<Email>(UserErrors.Email.InvalidFormat);
        }
        
        return new Email(value);
    }
    
    public static Email FromValue(string value)
    {
        return new Email(value);
    }
}