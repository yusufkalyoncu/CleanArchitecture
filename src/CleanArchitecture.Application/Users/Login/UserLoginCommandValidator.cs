using CleanArchitecture.Domain.Users;
using FluentValidation;

namespace CleanArchitecture.Application.Users.Login;

internal sealed class UserLoginCommandValidator : AbstractValidator<UserLoginCommand>
{
    public UserLoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .WithErrorCode(UserErrors.Email.TooLong.ErrorCode);
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(Password.MinLength)
            .WithErrorCode(UserErrors.Password.TooShort.ErrorCode)
            .MaximumLength(Password.MaxLength)
            .WithErrorCode(UserErrors.Password.TooLong.ErrorCode);
    }
}