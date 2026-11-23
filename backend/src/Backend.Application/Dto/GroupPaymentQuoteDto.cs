namespace Backend.Application.Dto;

public sealed record GroupPaymentQuoteDto(string GroupId, DateTimeOffset Deadline, long PriceKopecks, long SavingsKopecks,
    long? SettlementPriceKopecks, bool FullRefundOnCancellation) {
    public long TotalKopecks { get; init; }
}
