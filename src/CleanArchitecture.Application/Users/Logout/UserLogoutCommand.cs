using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Logout;

public sealed record UserLogoutCommand : ICommand;

internal sealed class UserLogoutCommandHandler(
    IUserContext userContext,
    ISessionService sessionService) : ICommandHandler<UserLogoutCommand>
{
    public async Task<Result> Handle(
        UserLogoutCommand request,
        CancellationToken cancellationToken)
    {
        await sessionService.RevokeSessionAsync(
            userContext.Id,
            userContext.Jti,
            userContext.AccessTokenRemainingLifetime);

        return Result.Success();
    }
}