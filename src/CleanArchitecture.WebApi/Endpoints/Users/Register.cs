using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Infrastructure.RateLimiting;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class Register : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/register", async (
                [FromBody] UserRegisterCommand request,
                ICommandHandler<UserRegisterCommand, UserRegisterCommandResponse> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(request, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users)
            .RequireRateLimiting(RateLimitPolicies.Registration);
    }
}