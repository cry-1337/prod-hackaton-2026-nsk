namespace Backend.Api.Contracts.Responses;

public sealed record PaymentHistoryResponse(long NetChangeKopecks, IReadOnlyList<PaymentHistoryEntryResponse> Orders) {
    public IReadOnlyList<ReferralBonusResponse> ReferralBonuses { get; init; } = [];
}
