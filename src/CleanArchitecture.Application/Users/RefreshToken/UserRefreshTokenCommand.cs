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
    string RefreshToken,
    TimeSpan RemainingAccessTokenTtl) : ICommand<UserRefreshTokenCommandResponse>;

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
        if (await sessionService.IsTokenOnCooldownAsync(request.Jti))
        {
            return Result.Failure<UserRefreshTokenCommandResponse>(UserErrors.TokenOnCooldown);
        }
        
        var isConsumed = await sessionService.ConsumeRefreshTokenAsync(
            request.UserId, 
            request.Jti, 
            request.RefreshToken);

        if (!isConsumed)
        {
            return Result.Failure<UserRefreshTokenCommandResponse>(UserErrors.InvalidToken);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            await sessionService.DeleteRefreshTokenAsync(request.UserId, request.Jti);
            return Result.Failure<UserRefreshTokenCommandResponse>(UserErrors.InvalidToken);
        }
        
        await sessionService.BlacklistAccessTokenAsync(request.Jti, request.RemainingAccessTokenTtl);
        await sessionService.UnregisterSessionAsync(request.UserId, request.Jti);
        
        var (jti, accessToken) = tokenProvider.CreateAccessToken(user);
        var refreshToken = tokenProvider.CreateRefreshToken();
        
        await sessionService.StoreRefreshTokenAsync(user.Id, jti, refreshToken);
        await sessionService.StartTokenCooldownAsync(jti);
        await sessionService.RegisterSessionAsync(user.Id, jti);
        
        return new UserRefreshTokenCommandResponse(accessToken, refreshToken);
    }
}