using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public readonly record struct Name
{
    public const int FirstNameMinLength = 2;
    public const int FirstNameMaxLength = 50;
    public const int LastNameMinLength = 2;
    public const int LastNameMaxLength = 50;
    
    public string FirstName { get; }
    public string LastName { get; }
    
    private Name(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }
    
    public override string ToString() => $"{FirstName} {LastName}";
    public string FullName => ToString();

    public static Result<Name> Create(string firstName, string lastName)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Result.Failure<Name>(UserErrors.Name.FirstName.Empty);
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure<Name>(UserErrors.Name.LastName.Empty);
        }

        if(firstName.Length < FirstNameMinLength)
        {
            return Result.Failure<Name>(UserErrors.Name.FirstName.TooShort);
        }

        if(firstName.Length > FirstNameMaxLength)
        {
            return Result.Failure<Name>(UserErrors.Name.FirstName.TooLong);
        }

        if(lastName.Length < LastNameMinLength)
        {
            return Result.Failure<Name>(UserErrors.Name.LastName.TooShort);
        }

        if(lastName.Length > LastNameMaxLength)
        {
            return Result.Failure<Name>(UserErrors.Name.LastName.TooLong);
        }

        return new Name(firstName, lastName);
    }
}