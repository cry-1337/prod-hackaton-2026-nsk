using Backend.Domain.Groups;

namespace Backend.Application.Queries.Account;

internal static class OrderNotifications {
    public static IQueryable<GroupOrderEntity> ForUser(this IQueryable<GroupOrderEntity> orders, Guid userId) =>
        orders.Where(order => order.Participant.UserId == userId &&
            (order.Participant.GroupPurchase.Status == EGroupPurchaseStatus.Successful ||
             (order.RefundedAt != null && order.PaidKopecks > 0)));
}
