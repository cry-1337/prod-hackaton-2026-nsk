namespace Backend.Application.Dto;

public sealed record GroupLimitQuoteDto(
    string GroupId,
    DateTimeOffset Deadline,
    long LimitKopecks,
    long MinimumPriceKopecks,
    bool BelowMinimum,
    long? ReachablePriceKopecks,
    long ChargeNowKopecks,
    int OtherLimitsPurchased) {
    public decimal? RemainingQuantity { get; init; }
    public bool CanAfford { get; init; }
    public long RefundNowKopecks { get; init; }
}
