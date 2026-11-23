namespace Backend.Api.Contracts.Responses;

public sealed record GroupPaymentQuoteResponse(string GroupId, DateTimeOffset Deadline, long PriceKopecks, long SavingsKopecks,
    long? SettlementPriceKopecks, bool FullRefundOnCancellation) {
    public long TotalKopecks { get; init; }
}
