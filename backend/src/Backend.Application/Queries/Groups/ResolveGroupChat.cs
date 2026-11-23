using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record ResolveGroupChatQuery(string Identifier) : IQuery<Guid>;

public sealed class ResolveGroupChatHandler(IAppDbContext dbContext) : IQueryHandler<ResolveGroupChatQuery, Guid> {
    public async ValueTask<Guid> Handle(ResolveGroupChatQuery query, CancellationToken cancellationToken) =>
        await dbContext.GroupPurchases.ByIdentifier(query.Identifier).Select(group => (Guid?)group.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
}
