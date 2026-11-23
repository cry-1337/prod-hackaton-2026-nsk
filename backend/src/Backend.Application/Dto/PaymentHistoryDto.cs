namespace Backend.Application.Dto;

public sealed record PaymentHistoryDto(long NetChangeKopecks, IReadOnlyList<PaymentHistoryEntryDto> Orders) {
    public IReadOnlyList<ReferralBonusDto> ReferralBonuses { get; init; } = [];
}
