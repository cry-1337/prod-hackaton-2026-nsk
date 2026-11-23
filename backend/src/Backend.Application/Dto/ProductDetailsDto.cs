using Backend.Domain.Catalog;
using Backend.Domain.Groups;

namespace Backend.Application.Dto;

public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    string? ImageKey,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    IReadOnlyList<PriceTierDto> PriceTiers,
    ProductBonusDto OrganizerBonus,
    IReadOnlyList<EGroupPurchaseReward> RewardOptions,
    IReadOnlyList<GroupPurchaseDto> OpenGroups);
