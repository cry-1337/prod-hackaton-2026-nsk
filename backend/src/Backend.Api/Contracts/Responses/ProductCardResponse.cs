using Backend.Domain.Catalog;

namespace Backend.Api.Contracts.Responses;

public sealed record ProductCardResponse(
    Guid Id,
    string Name,
    string? ImageUrl,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    PriceTierResponse? StartingTier,
    int OpenGroupsCount) {
    public int? TopOrganizerLikesCount { get; init; }
}
