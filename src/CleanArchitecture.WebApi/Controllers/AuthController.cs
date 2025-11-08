using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Application.Users.RefreshToken;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IStringLocalizer<Lang> localizer) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IResult> Register(
        [FromBody] UserRegisterCommand command,
        ICommandHandler<UserRegisterCommand, UserRegisterCommandResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToOk(localizer);
    }
    
    [HttpPost("login")]
    public async Task<IResult> Login(
        [FromBody] UserLoginCommand command,
        ICommandHandler<UserLoginCommand, UserLoginCommandResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToOk(localizer);
    }
    
    [Authorize]
    [HttpPost("logout")]
    public async Task<IResult> Logout(
        ICommandHandler<UserLogoutCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new UserLogoutCommand(
            User.GetUserId(),
            User.GetJti(),
            User.GetAccessTokenRemainingLifetime());
        
        var result = await handler.Handle(command, cancellationToken);
        return result.ToOk(localizer);
    }
    
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IResult> LogoutAll(
        ICommandHandler<UserLogoutAllCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new UserLogoutAllCommand(
            User.GetUserId());
        
        var result = await handler.Handle(command, cancellationToken);
        return result.ToOk(localizer);
    }
    
    [Authorize(AuthenticationSchemes = "BearerIgnoreLifetime")]
    [HttpPost("refresh-token")]
    public async Task<IResult> RefreshToken(
        [FromBody] UserRefreshTokenCommandRequest request,
        ICommandHandler<UserRefreshTokenCommand, UserRefreshTokenCommandResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new UserRefreshTokenCommand(
            User.GetUserId(),
            User.GetJti(),
            request.RefreshToken,
            User.GetAccessTokenRemainingLifetime());
        
        var result = await handler.Handle(command, cancellationToken);
        return result.ToOk(localizer);
    }
}