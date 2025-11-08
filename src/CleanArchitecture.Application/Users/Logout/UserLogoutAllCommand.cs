using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Logout;

public sealed record UserLogoutAllCommand(Guid UserId) : ICommand;

internal sealed class UserLogoutAllCommandHandler(
    ISessionService sessionService) : ICommandHandler<UserLogoutAllCommand>
{
    public async Task<Result> Handle(
        UserLogoutAllCommand request,
        CancellationToken cancellationToken)
    {
        await sessionService.BlacklistAllUserSessionsAsync(request.UserId);
        return Result.Success();
    }
}