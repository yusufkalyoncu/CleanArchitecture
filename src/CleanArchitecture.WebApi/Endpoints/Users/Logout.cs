using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/logout", async (
                ICommandHandler<UserLogoutCommand> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var command = new UserLogoutCommand();
                var result = await handler.Handle(command, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users)
            .RequireAuthorization();
    }
}