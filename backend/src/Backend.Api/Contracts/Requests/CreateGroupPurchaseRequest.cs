using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Requests;

public sealed record CreateGroupPurchaseRequest(
    Guid ProductId,
    DateTimeOffset Deadline,
    EGroupPurchaseReward? RewardKind = null,
    int? TargetParticipants = null,
    long? OrganizerLimitKopecks = null
) {
    public decimal Quantity { get; init; } = 1m;
    public Guid? CardId { get; init; }
    public DeliveryAddressRequest Address { get; init; } = null!;
    public long? ConfirmedPriceKopecks { get; init; }
}
