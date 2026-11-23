namespace Backend.Application.Dto;

public sealed record PaymentHistoryEntryDto(string GroupId, Guid OrderId, long PaidKopecks, DateTimeOffset PaidAt,
    DateTimeOffset? RefundedAt, DateTimeOffset? CashbackAt) {
    public long PriceRefundKopecks { get; init; }
}
