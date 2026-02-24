using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Users.RefreshToken;

public sealed record UserRefreshTokenCommandRequest(string RefreshToken);

public sealed record UserRefreshTokenCommand(
    Guid UserId,
    string Jti,
    string RefreshToken) : ICommand<UserRefreshTokenCommandResponse>;

public sealed record UserRefreshTokenCommandResponse(string AccessToken, string RefreshToken);

internal sealed class UserRefreshTokenCommandHandler(
    IApplicationDbContext dbContext,
    ITokenProvider tokenProvider,
    ISessionService sessionService) : ICommandHandler<UserRefreshTokenCommand, UserRefreshTokenCommandResponse>
{
    public async Task<Result<UserRefreshTokenCommandResponse>> Handle(
        UserRefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var consumeRefreshTokenResult = await sessionService.ConsumeRefreshTokenAsync(
            request.UserId, 
            request.Jti, 
            request.RefreshToken);

        if (!consumeRefreshTokenResult.IsSuccess)
        {
            return Result.Failure<UserRefreshTokenCommandResponse>(UserErrors.InvalidToken);
        }

        if (!string.IsNullOrEmpty(consumeRefreshTokenResult.CachedAccessToken) &&
            !string.IsNullOrEmpty(consumeRefreshTokenResult.CachedRefreshToken))
        {
            return new UserRefreshTokenCommandResponse(
                consumeRefreshTokenResult.CachedAccessToken,
                consumeRefreshTokenResult.CachedRefreshToken);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserRefreshTokenCommandResponse>(UserErrors.InvalidToken);
        }

        var (newJti, newAccessToken) = tokenProvider.CreateAccessToken(user);
        var newRefreshToken = tokenProvider.CreateRefreshToken();

        await sessionService.RotateSessionAsync(
            request.UserId,
            request.Jti,
            newJti,
            newAccessToken,
            newRefreshToken);
        
        return new UserRefreshTokenCommandResponse(newAccessToken, newRefreshToken);
    }
}