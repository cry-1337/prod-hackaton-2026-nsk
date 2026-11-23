namespace Backend.Api.Contracts.Responses;

public sealed record BalanceResponse(
    long BalanceKopecks,
    long ToppedUpKopecks,
    long PaidKopecks,
    long RefundedKopecks,
    long CashbackKopecks,
    long ReferralBonusKopecks
);
