using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public sealed record Name
{
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
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Result.Failure<Name>(NameErrors.FirstNameCannotBeNullOrEmpty);
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure<Name>(NameErrors.LastNameCannotBeNullOrEmpty);
        }

        return new Name(firstName.Trim(), lastName.Trim());
    }
}