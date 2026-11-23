using System.Text.Json.Serialization;
using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Responses;

public sealed record GroupLimitResponse(long LimitKopecks, EGroupLimitStatus Status, long ChargedKopecks, Guid? OrderId) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeliveryAddress { get; init; }

    public long RefundedKopecks { get; init; }
    public decimal? RemainingQuantity { get; init; }
    public DeliveryAddress? Address { get; init; }
    public decimal Quantity { get; init; } = 1m;
}
