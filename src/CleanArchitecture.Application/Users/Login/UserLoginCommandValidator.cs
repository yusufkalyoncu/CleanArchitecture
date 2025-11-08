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
            .WithErrorCode(EmailErrors.LengthExceeded.ErrorCode);
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(Password.MinLength)
            .MaximumLength(Password.MaxLength)
            .WithErrorCode(PasswordErrors.InvalidLength.ErrorCode);
    }
}