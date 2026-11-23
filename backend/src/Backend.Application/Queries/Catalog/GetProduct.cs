using Backend.Application.Commands.Groups;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Catalog;

public sealed record GetProductQuery(Guid ProductId) : IQuery<ProductDetailsDto?>;

public sealed class GetProductHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetProductQuery, ProductDetailsDto?> {
    public async ValueTask<ProductDetailsDto?> Handle(GetProductQuery query, CancellationToken cancellationToken) {
        var now = clock.GetUtcNow();
        var product = await dbContext.Products
            .AsNoTrackingWithIdentityResolution()
            .Include(product => product.PriceTiers)
            .Include(product => product.GroupPurchases.Where(group =>
                group.Status == EGroupPurchaseStatus.Open && group.Deadline > now))
                .ThenInclude(group => group.Organizer)
                    .ThenInclude(organizer => organizer.OrganizerLikes)
            .Include(product => product.GroupPurchases)
                .ThenInclude(group => group.PriceTiers)
            .Include(product => product.GroupPurchases)
                .ThenInclude(group => group.Participants)
                    .ThenInclude(participant => participant.Order)
            .AsSplitQuery()
            .SingleOrDefaultAsync(product => product.Id == query.ProductId, cancellationToken);

        if (product is null) {
            return null;
        }

        var priceTiers = product.PriceTiers.OrderBy(tier => tier.RequiredParticipants)
            .Select(tier => new PriceTierDto(tier.RequiredParticipants, tier.PriceKopecks))
            .ToArray();
        var balances = await dbContext.GetGroupWalletBalancesAsync(product.GroupPurchases, null, cancellationToken);
        var groups = product.GroupPurchases
            .OrderByDescending(group => group.Organizer.OrganizerLikes.Count)
            .ThenBy(group => group.Deadline)
            .ThenBy(group => group.Id)
            .Select(group => group.ToDto(now, balances))
            .ToArray();

        return new ProductDetailsDto(
            product.Id,
            product.Name,
            product.Description,
            product.ImageKey,
            product.RegularPriceKopecks,
            product.SaleUnit,
            priceTiers,
            new ProductBonusDto(product.BonusQuantity, product.BonusUnit),
            priceTiers.Length > 0 ? [EGroupPurchaseReward.FreeProduct] : [],
            groups);
    }
}
