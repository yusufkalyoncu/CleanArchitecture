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
            .WithErrorCode(UserErrors.Email.TooLong.ErrorCode);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MinimumLength(Name.FirstNameMinLength)
            .WithErrorCode(UserErrors.Name.FirstName.TooShort.ErrorCode)
            .MaximumLength(Name.FirstNameMaxLength)
            .WithErrorCode(UserErrors.Name.FirstName.TooLong.ErrorCode);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MinimumLength(Name.LastNameMinLength)
            .WithErrorCode(UserErrors.Name.LastName.TooShort.ErrorCode)
            .MaximumLength(Name.LastNameMaxLength)
            .WithErrorCode(UserErrors.Name.LastName.TooLong.ErrorCode);
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(Password.MinLength)
            .WithErrorCode(UserErrors.Password.TooShort.ErrorCode)
            .MaximumLength(Password.MaxLength)
            .WithErrorCode(UserErrors.Password.TooLong.ErrorCode);
    }
}