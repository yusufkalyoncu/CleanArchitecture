using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Users.Register;

public sealed record UserRegisterCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password) : ICommand<UserRegisterCommandResponse>;

public sealed record UserRegisterCommandResponse(string AccessToken, string RefreshToken);

internal sealed class UserRegisterCommandHandler(
    IApplicationDbContext dbContext,
    ITokenProvider tokenProvider,
    ISessionService sessionService) : ICommandHandler<UserRegisterCommand, UserRegisterCommandResponse>
{
    public async Task<Result<UserRegisterCommandResponse>> Handle(
        UserRegisterCommand request,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<UserRegisterCommandResponse>(emailResult.Error);
        }
        
        var userExists = await dbContext.Users
            .AnyAsync(u => u.Email == emailResult.Data, cancellationToken);

        if (userExists)
        {
            return Result.Failure<UserRegisterCommandResponse>(UserErrors.AlreadyExists);
        }

        var userResult = User.Create(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password);

        if (userResult.IsFailure)
        {
            return Result.Failure<UserRegisterCommandResponse>(userResult.Error);
        }

        await dbContext.Users.AddAsync(userResult.Data, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (jti, accessToken) = tokenProvider.CreateAccessToken(userResult.Data);
        var refreshToken = tokenProvider.CreateRefreshToken();

        await sessionService.StoreRefreshTokenAsync(userResult.Data.Id, jti, refreshToken);
        await sessionService.StartTokenCooldownAsync(jti);
        await sessionService.RegisterSessionAsync(userResult.Data.Id, jti);

        return new UserRegisterCommandResponse(accessToken, refreshToken);
    }
}