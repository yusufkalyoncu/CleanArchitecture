using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.GetUsers;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users", async (
                IQueryHandler<GetUsersQuery, IEnumerable<GetUsersQueryResponse>> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var query = new GetUsersQuery();
                var result = await handler.Handle(query, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users);
    }
}