using Backend.Application.Commands.Groups;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Account;

public sealed record GetMyGroupsQuery(Guid UserId, EGroupRole? Role = null) : IQuery<IReadOnlyList<GroupPurchaseDto>>;

public sealed class GetMyGroupsHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetMyGroupsQuery, IReadOnlyList<GroupPurchaseDto>> {
    public async ValueTask<IReadOnlyList<GroupPurchaseDto>> Handle(
        GetMyGroupsQuery query,
        CancellationToken cancellationToken
    ) {
        var groups = dbContext.GroupPurchases.WithDetails().Where(group =>
            group.OrganizerId == query.UserId ||
            group.Participants.Any(participant => participant.UserId == query.UserId));

        if (query.Role == EGroupRole.Organizer) {
            groups = groups.OrganizedBy(query.UserId);
        }

        var result = await groups
            .OrderByDescending(group => group.CreatedAt)
            .ThenBy(group => group.Id)
            .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var balances = await dbContext.GetGroupWalletBalancesAsync(result, null, cancellationToken);
        return result.Select(group => {
            var participant = group.Participants.SingleOrDefault(person => person.UserId == query.UserId);
            return group.ToDto(now, balances) with {
                MyLimit = group.GetLimit(query.UserId, now, balances),
                MyPayment = query.Role is null ? group.GetPayment(query.UserId) : null,
                MyDirectPaidKopecks = participant is { LimitKopecks: null }
                    ? participant.Order?.PaidKopecks ?? participant.DirectPaidKopecks
                    : null,
                MyBonus = group.OrganizerId == query.UserId && group.BonusAwardedAt.HasValue
                    ? new ProductBonusDto(group.BonusQuantity!.Value, group.BonusUnit!)
                    : null
            };
        }).ToArray();
    }
}
