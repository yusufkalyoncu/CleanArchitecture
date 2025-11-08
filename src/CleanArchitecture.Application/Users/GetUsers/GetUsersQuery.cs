using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Users.GetUsers;

public sealed record GetUsersQueryResponse(Guid Id, string Name, string Email);

public sealed record GetUsersQuery : IQuery<IEnumerable<GetUsersQueryResponse>>;

internal sealed class GetUsersQueryHandler(
    IApplicationDbContext dbContext) : IQueryHandler<GetUsersQuery, IEnumerable<GetUsersQueryResponse>>
{
    public async Task<Result<IEnumerable<GetUsersQueryResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Select(x => new GetUsersQueryResponse(
                x.Id,
                x.Name.FullName,
                x.Email.Value))
            .ToListAsync(cancellationToken);
    }
}