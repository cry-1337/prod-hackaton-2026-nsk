using Backend.Domain.Groups;

namespace Backend.Application.Dto;

public sealed record GroupPaymentDto(Guid? OrderId, Guid? CardId, string? DeliveryAddress, long PaidKopecks,
    long RefundedKopecks, long CashbackKopecks, bool FreeProduct, bool Reserved) {
    public DeliveryAddress? Address { get; init; }
    public decimal Quantity { get; init; } = 1m;
}
