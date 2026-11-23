namespace Backend.Api.Contracts.Responses;

public sealed record WalletTopUpResponse(Guid Id, long AmountKopecks, DateTimeOffset CreatedAt);
