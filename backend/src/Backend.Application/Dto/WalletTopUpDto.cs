namespace Backend.Application.Dto;

public sealed record WalletTopUpDto(Guid Id, long AmountKopecks, DateTimeOffset CreatedAt);
