namespace Backend.Api.Contracts.Responses;

public sealed record PaymentHistoryEntryResponse(string GroupId, Guid OrderId, long PaidKopecks, DateTimeOffset PaidAt,
    DateTimeOffset? RefundedAt, DateTimeOffset? CashbackAt) {
    public long PriceRefundKopecks { get; init; }
}
