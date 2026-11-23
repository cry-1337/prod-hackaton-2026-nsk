using Backend.Application.Commands.Groups;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupsQuery(EGroupPurchaseStatus? Status, int Offset, int Limit) : IQuery<IReadOnlyList<GroupPurchaseDto>> {
    public Guid? OrganizerId { get; init; }
    public string? Search { get; init; }
    public int? EndsWithinHours { get; init; }
    public long? MaxPriceKopecks { get; init; }
    public bool SortByOrganizerLikes { get; init; } = true;
}

public sealed class GetGroupsHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupPurchaseDto>> {
    public async ValueTask<IReadOnlyList<GroupPurchaseDto>> Handle(GetGroupsQuery query, CancellationToken cancellationToken) {
        var now = clock.GetUtcNow();
        var groups = dbContext.GroupPurchases.WithDetails();
        if (query.OrganizerId is { } organizerId) {
            groups = groups.OrganizedBy(organizerId);
        }

        if (query.Status is { } status) {
            groups = groups.Where(group => (group.Status == EGroupPurchaseStatus.Open && group.Deadline <= now
                ? EGroupPurchaseStatus.Cancelled
                : group.Status) == status);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search)) {
            groups = groups.Where(group => EF.Functions.Collate(group.Product.Name, "und-x-icu").ToLower().Contains(search) ||
                EF.Functions.Collate(group.Organizer.Nickname, "und-x-icu").ToLower().Contains(search) ||
                group.InviteCode.Contains(search));
        }

        if (query.MaxPriceKopecks is { } maxPrice) {
            groups = groups.Where(group => group.SettlementPriceKopecks.HasValue
                ? group.SettlementPriceKopecks <= maxPrice
                : group.PriceTiers.Any(tier => (group.TargetParticipants == null || tier.RequiredParticipants == group.TargetParticipants)
                    && tier.PriceKopecks <= maxPrice));
        }

        if (query.EndsWithinHours is { } hours) {
            var end = now.AddHours(hours);
            groups = groups.Where(group => group.Status == EGroupPurchaseStatus.Open && group.Deadline > now && group.Deadline <= end);
        }

        var ordered = query.SortByOrganizerLikes
            ? groups.OrderByOrganizerLikes()
            : groups.OrderByDescending(group => group.CreatedAt).ThenBy(group => group.Id);

        var page = await ordered
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        var balances = await dbContext.GetGroupWalletBalancesAsync(page, null, cancellationToken);
        return page.Select(group => group.ToDto(now, balances)).ToArray();
    }
}
