using Backend.Domain.Catalog;
using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Responses;

public sealed record ProductDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    IReadOnlyList<PriceTierResponse> PriceTiers,
    ProductBonusResponse OrganizerBonus,
    IReadOnlyList<EGroupPurchaseReward> RewardOptions,
    IReadOnlyList<GroupPurchaseResponse> OpenGroups);
