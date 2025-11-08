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
        await sessionService.BlacklistAccessTokenAsync(request.Jti, request.RemainingAccessTokenTtl);
        await sessionService.DeleteRefreshTokenAsync(request.UserId, request.Jti);
        await sessionService.UnregisterSessionAsync(request.UserId, request.Jti);
        return Result.Success();
    }
}