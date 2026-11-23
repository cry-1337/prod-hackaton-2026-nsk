using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Responses;

public sealed record GroupPaymentResponse(Guid? OrderId, Guid? CardId, string? DeliveryAddress, long PaidKopecks,
    long RefundedKopecks, long CashbackKopecks, bool FreeProduct, bool Reserved) {
    public DeliveryAddress? Address { get; init; }
    public decimal Quantity { get; init; } = 1m;
}
