using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Auth;

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<UserDto?>;

public sealed class GetCurrentUserHandler(IAppDbContext dbContext) : IQueryHandler<GetCurrentUserQuery, UserDto?> {
    public async ValueTask<UserDto?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == query.UserId)
            .Select(user => new UserDto(user.Id, user.Email, user.Nickname))
            .SingleOrDefaultAsync(cancellationToken);
}
