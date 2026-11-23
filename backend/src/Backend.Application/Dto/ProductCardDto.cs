using Backend.Domain.Catalog;

namespace Backend.Application.Dto;

public sealed record ProductCardDto(
    Guid Id,
    string Name,
    string? ImageKey,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    PriceTierDto? StartingTier,
    int OpenGroupsCount) {
    public int? TopOrganizerLikesCount { get; init; }
}
