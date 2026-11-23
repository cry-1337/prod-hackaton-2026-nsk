using Backend.Domain.Catalog;
using System.Text.Json.Serialization;
using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Responses;

public sealed record GroupPurchaseResponse(
    string Id,
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    long RegularPriceKopecks,
    ESaleUnit SaleUnit,
    Guid OrganizerId,
    string OrganizerNickname,
    DateTimeOffset Deadline,
    DateTimeOffset CreatedAt,
    EGroupPurchaseStatus Status,
    EGroupPurchaseReward RewardKind,
    int ParticipantsCount,
    IReadOnlyList<PriceTierResponse> PriceTiers) {
    public string InviteUrl { get; init; } = null!;

    public string InviteCode => Id;

    public decimal EligibleQuantity { get; init; }
    public int EligibleParticipantsCount { get; init; }
    public int DirectPaymentsCount { get; init; }
    public PriceTierResponse? ProgressTier { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProductBonusResponse? MyBonus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MyDirectPaidKopecks { get; init; }

    public int OrganizerLikesCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PendingLimitsCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PurchasedLimitsCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SettlementPriceKopecks { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroupLimitResponse? MyLimit { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroupPaymentResponse? MyPayment { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EGroupRole? MyRole { get; init; }
}
