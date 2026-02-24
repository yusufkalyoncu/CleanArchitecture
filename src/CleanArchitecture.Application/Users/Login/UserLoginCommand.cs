using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Users.Login;

public sealed record UserLoginCommand(string Email, string Password) : ICommand<UserLoginCommandResponse>;

public sealed record UserLoginCommandResponse(string AccessToken, string RefreshToken);

internal sealed class UserLoginCommandHandler(
    IApplicationDbContext dbContext,
    ITokenProvider tokenProvider,
    ISessionService sessionService) : ICommandHandler<UserLoginCommand, UserLoginCommandResponse>
{
    public async Task<Result<UserLoginCommandResponse>> Handle(
        UserLoginCommand request,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<UserLoginCommandResponse>(emailResult.Error);
        }
        
        var user = await dbContext.Users
            .Where(x => x.Email == emailResult.Data)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserLoginCommandResponse>(UserErrors.InvalidCredentials);
        }

        var passwordValid = user.Password.VerifyPassword(request.Password);
        
        if (!passwordValid)
        {
            return Result.Failure<UserLoginCommandResponse>(UserErrors.InvalidCredentials);
        }
        
        var (jti, accessToken) = tokenProvider.CreateAccessToken(user);
        var refreshToken = tokenProvider.CreateRefreshToken();

        var sessionResult = await sessionService.CreateLoginSessionAsync(
            user.Id,
            jti,
            refreshToken);

        if (sessionResult.IsFailure)
        {
            return Result.Failure<UserLoginCommandResponse>(sessionResult.Error);
        }
        
        return new UserLoginCommandResponse(accessToken, refreshToken);
    }
}