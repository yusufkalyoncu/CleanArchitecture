using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Logout;

public sealed record UserLogoutCommand(
    Guid UserId,
    string Jti,
    TimeSpan RemainingAccessTokenTtl) : ICommand;

internal sealed class UserLogoutCommandHandler(
    ISessionService sessionService) : ICommandHandler<UserLogoutCommand>
{
    public async Task<Result> Handle(
        UserLogoutCommand request,
        CancellationToken cancellationToken)
    {
        await sessionService.RevokeSessionAsync(request.UserId, request.Jti, request.RemainingAccessTokenTtl);
        return Result.Success();
    }
}