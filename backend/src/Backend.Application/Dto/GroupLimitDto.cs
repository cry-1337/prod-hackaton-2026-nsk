using Backend.Domain.Groups;

namespace Backend.Application.Dto;

public sealed record GroupLimitDto(
    long LimitKopecks,
    EGroupLimitStatus Status,
    long ChargedKopecks,
    Guid? OrderId) {
    public string? DeliveryAddress { get; init; }
    public long RefundedKopecks { get; init; }
    public decimal? RemainingQuantity { get; init; }
    public DeliveryAddress? Address { get; init; }
    public decimal Quantity { get; init; } = 1m;
}
