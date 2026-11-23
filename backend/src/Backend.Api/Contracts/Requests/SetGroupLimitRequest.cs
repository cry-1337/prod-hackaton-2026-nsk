namespace Backend.Api.Contracts.Requests;

public sealed record SetGroupLimitRequest(long LimitKopecks) {
    public decimal Quantity { get; init; } = 1m;
    public Guid? CardId { get; init; }
    public DeliveryAddressRequest Address { get; init; } = null!;
}
