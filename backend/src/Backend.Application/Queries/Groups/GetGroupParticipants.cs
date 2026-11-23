using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupParticipantsQuery(string Identifier, int Offset, int Limit) : IQuery<IReadOnlyList<GroupParticipantDto>>;

public sealed class GetGroupParticipantsHandler(IAppDbContext dbContext)
    : IQueryHandler<GetGroupParticipantsQuery, IReadOnlyList<GroupParticipantDto>> {
    public async ValueTask<IReadOnlyList<GroupParticipantDto>> Handle(GetGroupParticipantsQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases.ByIdentifier(query.Identifier)
            .Select(item => new {
                item.Id, item.OrganizerId, item.SettlementPriceKopecks,
                StartingPrice = item.PriceTiers.Where(tier => item.TargetParticipants == null || tier.RequiredParticipants == item.TargetParticipants)
                    .Max(tier => (long?)tier.PriceKopecks)
            })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");

        return await dbContext.GroupPurchaseParticipants
            .Where(member => member.GroupPurchaseId == group.Id && !member.LimitRevoked)
            .OrderByDescending(member => member.UserId == group.OrganizerId)
            .ThenBy(member => member.JoinedAt)
            .ThenBy(member => member.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(member => new GroupParticipantDto(
                member.UserId,
                member.User.Nickname,
                member.Order != null && member.Order.PaidKopecks == 0 ? 0L :
                    member.Order != null && group.SettlementPriceKopecks != null ? group.SettlementPriceKopecks :
                    member.LimitKopecks ?? (member.Order != null || member.DirectPaidKopecks != null ? group.StartingPrice : null),
                member.LimitKopecks != null && member.Order == null))
            .ToArrayAsync(cancellationToken);
    }
}
