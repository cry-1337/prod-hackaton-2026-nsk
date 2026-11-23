using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Catalog;

public sealed record GetProductsQuery(string? Search) : IQuery<IReadOnlyList<ProductCardDto>> {
    public bool SortByOrganizerLikes { get; init; }
}

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery> {
    public GetProductsQueryValidator() {
        RuleFor(query => query.Search).MaximumLength(256).WithErrorCode("SEARCH_TOO_LONG");
    }
}

public sealed class GetProductsHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetProductsQuery, IReadOnlyList<ProductCardDto>> {
    public async ValueTask<IReadOnlyList<ProductCardDto>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken
    ) {
        var now = clock.GetUtcNow();
        var products = dbContext.Products.AsNoTracking();
        var search = query.Search?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(search)) {
            products = products.Where(product =>
                EF.Functions.Collate(product.Name, "und-x-icu").ToLower().Contains(search));
        }

        var ranked = products.Select(product => new {
            Product = product,
            TopLikes = product.GroupPurchases
                .Where(group => group.Status == EGroupPurchaseStatus.Open && group.Deadline > now)
                .Max(group => (int?)group.Organizer.OrganizerLikes.Count)
        });

        var ordered = query.SortByOrganizerLikes
            ? ranked.OrderByDescending(row => row.TopLikes ?? -1).ThenBy(row => row.Product.Name)
            : ranked.OrderBy(row => row.Product.Name);

        return await ordered
            .ThenBy(row => row.Product.Id)
            .Select(row => new ProductCardDto(
                row.Product.Id,
                row.Product.Name,
                row.Product.ImageKey,
                row.Product.RegularPriceKopecks,
                row.Product.SaleUnit,
                row.Product.PriceTiers.OrderBy(tier => tier.RequiredParticipants)
                    .Select(tier => new PriceTierDto(tier.RequiredParticipants, tier.PriceKopecks))
                    .FirstOrDefault(),
                row.Product.GroupPurchases.Count(group =>
                    group.Status == EGroupPurchaseStatus.Open && group.Deadline > now)) {
                TopOrganizerLikesCount = row.TopLikes
            })
            .ToListAsync(cancellationToken);
    }
}
