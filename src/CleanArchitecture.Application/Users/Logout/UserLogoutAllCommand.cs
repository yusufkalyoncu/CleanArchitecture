using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Logout;

public sealed record UserLogoutAllCommand : ICommand;

internal sealed class UserLogoutAllCommandHandler(
    IUserContext userContext,
    ISessionService sessionService) : ICommandHandler<UserLogoutAllCommand>
{
    public async Task<Result> Handle(
        UserLogoutAllCommand request,
        CancellationToken cancellationToken)
    {
        await sessionService.RevokeAllSessionsAsync(userContext.Id);
        return Result.Success();
    }
}