using FluentValidation;

namespace CleanArchitecture.Application.Users.RefreshToken;

internal sealed class UserRefreshTokenCommandValidator : AbstractValidator<UserRefreshTokenCommand>
{
    public UserRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithErrorCode("RefreshToken.Empty");
    }
}