using CleanArchitecture.Domain.Users;
using FluentValidation;

namespace CleanArchitecture.Application.Users.Register;

internal sealed class UserRegisterCommandValidator : AbstractValidator<UserRegisterCommand>
{
    public UserRegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .WithErrorCode(EmailErrors.LengthExceeded.ErrorCode);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MinimumLength(Name.FirstNameMinLength)
            .MaximumLength(Name.FirstNameMaxLength)
            .WithErrorCode(NameErrors.FirstNameLengthInvalid.ErrorCode);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MinimumLength(Name.LastNameMinLength)
            .MaximumLength(Name.LastNameMaxLength)
            .WithErrorCode(NameErrors.LastNameLengthInvalid.ErrorCode);
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(Password.MinLength)
            .MaximumLength(Password.MaxLength)
            .WithErrorCode(PasswordErrors.InvalidLength.ErrorCode);
    }
}