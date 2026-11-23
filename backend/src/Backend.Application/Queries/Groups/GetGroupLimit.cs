using Backend.Application.Commands.Groups;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupLimitQuery(string Identifier, Guid UserId) : IQuery<GroupLimitDto?>;

public sealed class GetGroupLimitHandler(IAppDbContext dbContext, TimeProvider clock) : IQueryHandler<GetGroupLimitQuery, GroupLimitDto?> {
    public async ValueTask<GroupLimitDto?> Handle(GetGroupLimitQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases.WithDetails().ByIdentifier(query.Identifier)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
        var balances = await dbContext.GetGroupWalletBalancesAsync([group], null, cancellationToken);
        return group.GetLimit(query.UserId, clock.GetUtcNow(), balances);
    }
}
