using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Account;

public sealed record GetOrderNotificationsQuery(Guid UserId, int Offset, int Limit, bool UnreadOnly)
    : IQuery<IReadOnlyList<OrderNotificationDto>>;

public sealed class GetOrderNotificationsHandler(IAppDbContext dbContext)
    : IQueryHandler<GetOrderNotificationsQuery, IReadOnlyList<OrderNotificationDto>> {
    public async ValueTask<IReadOnlyList<OrderNotificationDto>> Handle(
        GetOrderNotificationsQuery query,
        CancellationToken cancellationToken
    ) => await dbContext.GroupOrders.AsNoTracking().ForUser(query.UserId)
        .Where(order => !query.UnreadOnly || order.NotificationReadAt == null)
        .Select(order => new {
            Order = order,
            CreatedAt = order.RefundedAt ?? order.Participant.GroupPurchase.SuccessfulAt ??
                order.Participant.GroupPurchase.BonusAwardedAt ?? order.PaidAt
        })
        .OrderByDescending(notification => notification.CreatedAt)
        .ThenByDescending(notification => notification.Order.ParticipantId)
        .Skip(query.Offset)
        .Take(query.Limit)
        .Select(notification => new OrderNotificationDto(
            notification.Order.ParticipantId,
            notification.Order.RefundedAt == null ? EOrderNotificationKind.Purchase : EOrderNotificationKind.Refund,
            notification.Order.Participant.GroupPurchase.InviteCode,
            notification.Order.Participant.GroupPurchase.ProductId,
            notification.Order.Participant.GroupPurchase.Product.Name,
            notification.Order.PaidKopecks - notification.Order.PriceRefundKopecks,
            notification.CreatedAt,
            notification.Order.NotificationReadAt))
        .ToArrayAsync(cancellationToken);
}
