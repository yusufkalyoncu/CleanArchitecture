using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.GetUsers;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Endpoints.Users;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{id:guid}", async (
                Guid id,
                IQueryHandler<GetUserQuery, GetUserQueryResponse> handler,
                IStringLocalizer<Lang> localizer,
                CancellationToken cancellationToken) =>
            {
                var query = new GetUserQuery(id);
                var result = await handler.Handle(query, cancellationToken);
                return result.ToOk(localizer);
            })
            .WithTags(Tags.Users);
    }
}