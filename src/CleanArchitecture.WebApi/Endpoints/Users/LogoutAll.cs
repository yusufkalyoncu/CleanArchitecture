using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class LogoutAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/logout-all", async (
                ICommandHandler<UserLogoutAllCommand> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var command = new UserLogoutAllCommand();
                var result = await handler.Handle(command, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users)
            .RequireAuthorization();
    }
}