using Backend.Domain.Groups;

namespace Backend.Application.Dto;

public sealed record OrderNotificationDto(
    Guid Id,
    EOrderNotificationKind Kind,
    string GroupId,
    Guid ProductId,
    string ProductName,
    long AmountKopecks,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt
);
