using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Users.GetUsers;

public sealed record GetUserQueryResponse(Guid Id, string Name, string Email);

public sealed record GetUserQuery(Guid UserId) : IQuery<GetUserQueryResponse>;

internal sealed class GetUserQueryHandler(
    IApplicationDbContext dbContext) : IQueryHandler<GetUserQuery, GetUserQueryResponse>
{
    public async Task<Result<GetUserQueryResponse>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(x => new GetUserQueryResponse(
                x.Id,
                x.Name.FullName,
                x.Email.Value))
            .FirstOrDefaultAsync(cancellationToken);
    }
}