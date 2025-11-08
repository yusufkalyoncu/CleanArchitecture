using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.GetUsers;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public sealed class UserController(IStringLocalizer<Lang> localizer) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetUsers(
        IQueryHandler<GetUsersQuery, IEnumerable<GetUsersQueryResponse>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery();
        var result = await handler.Handle(query, cancellationToken);
        return result.ToOk(localizer);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IResult> GetUser(
        Guid userId,
        IQueryHandler<GetUserQuery, GetUserQueryResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(userId);
        var result = await handler.Handle(query, cancellationToken);
        return result.ToOk(localizer);
    }
}