namespace Backend.Application.Dto;

public sealed record BalanceDto(
    long ToppedUpKopecks,
    long PaidKopecks,
    long RefundedKopecks,
    long CashbackKopecks,
    long ReferralBonusKopecks
) {
    public long BalanceKopecks => ToppedUpKopecks + RefundedKopecks + CashbackKopecks + ReferralBonusKopecks - PaidKopecks;
}
