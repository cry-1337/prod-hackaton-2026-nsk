using Backend.Application.Commands.Groups;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupPurchaseQuery(string Identifier, bool InviteCodeOnly = false) : IQuery<GroupPurchaseDto?>;

public sealed class GetGroupPurchaseHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetGroupPurchaseQuery, GroupPurchaseDto?> {
    public async ValueTask<GroupPurchaseDto?> Handle(GetGroupPurchaseQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases
            .WithDetails()
            .ByIdentifier(query.Identifier, query.InviteCodeOnly)
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null) {
            return null;
        }

        var balances = await dbContext.GetGroupWalletBalancesAsync([group], null, cancellationToken);
        return group.ToDto(clock.GetUtcNow(), balances);
    }
}
