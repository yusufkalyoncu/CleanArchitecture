using CleanArchitecture.Domain.Users.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public class User : Entity
{
    public Email Email { get; private set; }
    public Name Name { get; private set; } = null!;
    public Password Password { get; private set; }

    private User(Email email, Name name, Password password)
    {
        Email = email;
        Name = name;
        Password = password;
    }
    
    protected User(){}

    public static Result<User> Create(
        string email,
        string firstName,
        string lastName,
        string password)
    {
        var emailResult = Email.Create(email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<User>(emailResult.Error);
        }
        
        var nameResult = Name.Create(firstName, lastName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<User>(nameResult.Error);
        }

        var passwordResult = Password.Create(password);
        if (passwordResult.IsFailure)
        {
            return Result.Failure<User>(passwordResult.Error);
        }
        
        var user = new User(
            emailResult.Data,
            nameResult.Data,
            passwordResult.Data);
        
        user.Raise(new UserRegisteredDomainEvent(user.Id));
        
        return Result.Success(user);
    }
}