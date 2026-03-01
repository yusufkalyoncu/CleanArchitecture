using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.RefreshToken;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class RefreshToken : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/refresh-token", async (
                [FromBody] UserRefreshTokenCommandRequest request,
                ICommandHandler<UserRefreshTokenCommand, UserRefreshTokenCommandResponse> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var command = new UserRefreshTokenCommand(request.RefreshToken);
                var result = await handler.Handle(command, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users)
            .RequireAuthorization(AuthPolicies.RefreshToken);
    }
}