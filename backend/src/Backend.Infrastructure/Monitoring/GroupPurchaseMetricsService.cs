using Backend.Domain.Groups;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Backend.Infrastructure.Monitoring;

internal sealed class GroupPurchaseMetricsService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<GroupPurchaseMetricsService> logger
) : BackgroundService {
    private readonly Gauge m_Groups = Metrics.CreateGauge(
        "vmeste_groups", "Group purchases by persisted status.", ["status"]);
    private readonly Gauge m_Overdue = Metrics.CreateGauge(
        "vmeste_groups_overdue", "Group purchases still stored as open after their deadline.");
    private readonly Gauge m_Refunds = Metrics.CreateGauge(
        "vmeste_test_refunds", "Direct test payments in cancelled group purchases.");
    private readonly Gauge m_RefundAmount = Metrics.CreateGauge(
        "vmeste_test_refunds_rubles", "Value in rubles of direct test payments in cancelled group purchases.");
    private readonly Gauge m_LastSuccess = Metrics.CreateGauge(
        "vmeste_group_snapshot_last_success_timestamp_seconds", "Unix time of the last complete purchase metrics snapshot.");
    private readonly Gauge m_Participants = Metrics.CreateGauge(
        "vmeste_business_participants_total", "All group purchase participants.");
    private readonly Gauge m_Orders = Metrics.CreateGauge(
        "vmeste_business_orders_total", "All completed group purchase orders.");
    private readonly Gauge m_ChatMessages = Metrics.CreateGauge(
        "vmeste_feature_group_chat_messages_total", "All group chat messages.");
    private readonly Gauge m_ReferralInvites = Metrics.CreateGauge(
        "vmeste_feature_referral_invites_total", "All personal referral invites.");
    private readonly Gauge m_CoOrganizers = Metrics.CreateGauge(
        "vmeste_feature_co_organizers_total", "All group co-organizer assignments.");
    private readonly Gauge m_LimitOperations = Metrics.CreateGauge(
        "vmeste_feature_group_limit_operations_total", "All group limit operations.");
    private readonly Gauge m_DirectPayments = Metrics.CreateGauge(
        "vmeste_feature_direct_payment_operations_total", "All direct payment operations.");
    private readonly Gauge m_WalletTopUps = Metrics.CreateGauge(
        "vmeste_feature_wallet_topups_total", "All wallet top-ups.");

    private readonly Gauge m_LimitApplications = Metrics.CreateGauge(
        "vmeste_limit_applications", "Current outcome per group and buyer who used a purchase limit.", ["outcome"]);
    private readonly Gauge m_TimeToSuccess = Metrics.CreateGauge(
        "vmeste_group_time_to_success_seconds", "Lifetime time from creation to success of completed group purchases.", ["quantile"]);
    private readonly Gauge m_RefundCount = Metrics.CreateGauge(
        "vmeste_refunds", "Refund events by reason. Limit refund reasons are recorded since monitoring rollout.", ["reason"]);
    private readonly Gauge m_RefundRubles = Metrics.CreateGauge(
        "vmeste_refunds_rubles", "Rubles refunded by reason. Limit refund reasons are recorded since monitoring rollout.", ["reason"]);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        do {
            try {
                await RefreshAsync(cancellationToken);
            } catch (Exception exception) when (!cancellationToken.IsCancellationRequested) {
                logger.LogWarning(exception, "Failed to refresh group purchase metrics");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var groups = await db.GroupPurchases
            .GroupBy(group => group.Status)
            .Select(bucket => new {
                Status = bucket.Key,
                Count = bucket.LongCount(),
                Overdue = bucket.LongCount(group => group.Status == EGroupPurchaseStatus.Open && group.Deadline <= now)
            })
            .ToDictionaryAsync(row => row.Status, cancellationToken);

        var refunds = await db.GroupPurchaseParticipants
            .Where(person => person.LimitKopecks == null && (person.Order != null
                ? person.Order.RefundedAt != null
                : person.DirectPaidKopecks.HasValue && person.GroupPurchase.Status == EGroupPurchaseStatus.Cancelled))
            .GroupBy(person => 1)
            .Select(bucket => new {
                Count = bucket.LongCount(),
                Kopecks = bucket.Sum(person => (decimal)(person.Order != null ? person.Order.PaidKopecks : person.DirectPaidKopecks!.Value))
            })
            .SingleOrDefaultAsync(cancellationToken);

        var participants = await db.GroupPurchaseParticipants.LongCountAsync(cancellationToken);
        var orders = await db.GroupOrders.LongCountAsync(cancellationToken);
        var chatMessages = await db.GroupChatMessages.LongCountAsync(cancellationToken);
        var referralInvites = await db.GroupReferralInvites.LongCountAsync(cancellationToken);
        var coOrganizers = await db.GroupCoOrganizers.LongCountAsync(cancellationToken);
        var limitOperations = await db.GroupLimitOperations.LongCountAsync(cancellationToken);
        var directPayments = await db.GroupPaymentOperations.LongCountAsync(cancellationToken);
        var topUps = await db.WalletTopUps.LongCountAsync(cancellationToken);

        await RefreshCheckoutMetricsAsync(db, cancellationToken);

        foreach (var status in Enum.GetValues<EGroupPurchaseStatus>()) {
            m_Groups.WithLabels(status.ToString().ToLowerInvariant()).Set(groups.GetValueOrDefault(status)?.Count ?? 0);
        }

        m_Overdue.Set(groups.GetValueOrDefault(EGroupPurchaseStatus.Open)?.Overdue ?? 0);
        m_Refunds.Set(refunds?.Count ?? 0);
        m_RefundAmount.Set((double)((refunds?.Kopecks ?? 0) / 100m));
        m_LastSuccess.Set(clock.GetUtcNow().ToUnixTimeSeconds());
        m_Participants.Set(participants);
        m_Orders.Set(orders);
        m_ChatMessages.Set(chatMessages);
        m_ReferralInvites.Set(referralInvites);
        m_CoOrganizers.Set(coOrganizers);
        m_LimitOperations.Set(limitOperations);
        m_DirectPayments.Set(directPayments);
        m_WalletTopUps.Set(topUps);
    }

    private async Task RefreshCheckoutMetricsAsync(AppDbContext db, CancellationToken cancellationToken) {
        var outcomes = await db.GroupPurchaseParticipants
            .Where(person => person.LimitKopecks != null || person.LimitDebitsKopecks > 0)
            .GroupBy(person => person.LimitRevoked ? "revoked"
                : person.Order != null && person.Order.RefundedAt == null ? "purchased"
                : person.GroupPurchase.Status == EGroupPurchaseStatus.Open ? "pending"
                : person.GroupPurchase.Status == EGroupPurchaseStatus.Successful ? "unmatched"
                : person.LimitRefundsKopecks > 0 || person.Order != null && person.Order.RefundedAt != null ? "refunded" : "closed")
            .Select(bucket => new { Outcome = bucket.Key, Count = bucket.LongCount() })
            .ToDictionaryAsync(row => row.Outcome, row => row.Count, cancellationToken);

        var duration = await db.Database.SqlQuery<DurationQuantiles>($"""
            SELECT percentile_cont(0.50) WITHIN GROUP (ORDER BY extract(epoch FROM successful_at - created_at)) AS "Median",
                   percentile_cont(0.95) WITHIN GROUP (ORDER BY extract(epoch FROM successful_at - created_at)) AS "P95"
              FROM group_purchases
             WHERE status = {(int)EGroupPurchaseStatus.Successful} AND successful_at >= created_at
            """).SingleAsync(cancellationToken);

        var refunds = await db.Database.SqlQuery<RefundTotals>($"""
            SELECT reason AS "Reason", count(*) AS "Count", sum(amount_kopecks) AS "Kopecks"
              FROM (
                    SELECT reason, amount_kopecks FROM group_limit_refunds
                    UNION ALL
                    SELECT {(int)ELimitRefundReason.PriceDrop}, o.price_refund_kopecks
                      FROM group_limit_orders o
                     WHERE o.price_refund_kopecks > 0
                    UNION ALL
                    SELECT {(int)ELimitRefundReason.GroupCancelled}, coalesce(o.paid_kopecks, p.direct_paid_kopecks)
                      FROM group_purchase_participants p
                      JOIN group_purchases g ON g.id = p.group_purchase_id
                      LEFT JOIN group_limit_orders o ON o.participant_id = p.id
                     WHERE p.limit_kopecks IS NULL
                       AND (o.refunded_at IS NOT NULL OR (o.participant_id IS NULL AND g.status = {(int)EGroupPurchaseStatus.Cancelled}))
                       AND coalesce(o.paid_kopecks, p.direct_paid_kopecks) > 0
                   ) AS refunds
             GROUP BY reason
            """).ToDictionaryAsync(row => (ELimitRefundReason)row.Reason, cancellationToken);

        foreach (var outcome in new[] { "pending", "purchased", "revoked", "refunded", "unmatched", "closed" }) {
            m_LimitApplications.WithLabels(outcome).Set(outcomes.GetValueOrDefault(outcome));
        }

        m_TimeToSuccess.WithLabels("0.5").Set(duration.Median ?? double.NaN);
        m_TimeToSuccess.WithLabels("0.95").Set(duration.P95 ?? double.NaN);

        foreach (var reason in Enum.GetValues<ELimitRefundReason>()) {
            var label = reason switch {
                ELimitRefundReason.LimitChanged => "limit_changed",
                ELimitRefundReason.LimitRevoked => "limit_revoked",
                ELimitRefundReason.GroupCancelled => "group_cancelled",
                ELimitRefundReason.PriceDrop => "price_drop",
                ELimitRefundReason.Unmatched => "unmatched",
                ELimitRefundReason.DirectCheckout => "direct_checkout",
                _ => throw new ArgumentOutOfRangeException(nameof(reason))
            };

            var total = refunds.GetValueOrDefault(reason);
            m_RefundCount.WithLabels(label).Set(total?.Count ?? 0);
            m_RefundRubles.WithLabels(label).Set((double)((total?.Kopecks ?? 0) / 100m));
        }
    }

    private sealed record DurationQuantiles(double? Median, double? P95);

    private sealed record RefundTotals(int Reason, long Count, decimal Kopecks);
}
