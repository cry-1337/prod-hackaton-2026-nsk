using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Responses;

public sealed record OrderNotificationResponse(
    Guid Id,
    EOrderNotificationKind Kind,
    string GroupId,
    Guid ProductId,
    string ProductName,
    long AmountKopecks,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt
);
