using Backend.Domain.Catalog;
using Backend.Domain.Groups;

namespace Backend.Application.Dto;

public sealed record GroupPurchaseDto(
    string Id,
    Guid ProductId,
    string ProductName,
    string? ImageKey,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    Guid OrganizerId,
    string OrganizerNickname,
    DateTimeOffset Deadline,
    DateTimeOffset CreatedAt,
    EGroupPurchaseStatus Status,
    EGroupPurchaseReward RewardKind,
    int ParticipantsCount,
    IReadOnlyList<PriceTierDto> PriceTiers) {
    public int PendingLimitsCount { get; init; }
    public int PurchasedLimitsCount { get; init; }
    public decimal EligibleQuantity { get; init; }
    public int EligibleParticipantsCount { get; init; }
    public int DirectPaymentsCount { get; init; }
    public PriceTierDto? ProgressTier { get; init; }
    public ProductBonusDto? MyBonus { get; init; }
    public long? MyDirectPaidKopecks { get; init; }

    public int OrganizerLikesCount { get; init; }
    public long? SettlementPriceKopecks { get; init; }
    public GroupLimitDto? MyLimit { get; init; }
    public GroupPaymentDto? MyPayment { get; init; }
}
