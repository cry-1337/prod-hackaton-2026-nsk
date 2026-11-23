using Backend.Domain.Groups;
using Xunit;

namespace Backend.UnitTests;

public sealed class GroupPurchaseTests {
    private static readonly DateTimeOffset m_Now = DateTimeOffset.UnixEpoch;

    private static GroupPurchaseEntity CreateGroup() {
        var group = new GroupPurchaseEntity {
            OrganizerId = Guid.NewGuid(),
            Deadline = m_Now.AddHours(1),
            RewardKind = EGroupPurchaseReward.FreeProduct,
            BonusQuantity = 5,
            BonusUnit = "кг"
        };

        group.PriceTiers.Add(new GroupPurchasePriceTierEntity { RequiredParticipants = 3, PriceKopecks = 99000 });
        group.PriceTiers.Add(new GroupPurchasePriceTierEntity { RequiredParticipants = 5, PriceKopecks = 85000 });
        return group;
    }

    [Theory]
    [InlineData(84999, 0)]
    [InlineData(85000, 1)]
    [InlineData(99000, 1)]
    public void CountEligibleHonorsTheBuyersLimit(long limit, int expected) {
        var group = CreateGroup();
        group.AddParticipant(Guid.NewGuid(), m_Now, limit);

        Assert.Equal(expected, group.CountEligible(85000));
    }

    [Fact]
    public void PrepaidQuantitySelectsCheapestTierAndReturnsTheDifferenceOnce() {
        var group = CreateGroup();
        var buyer = group.AddParticipant(group.OrganizerId, m_Now, 99000);
        buyer.UsesWallet = true;
        buyer.Quantity = 5;
        buyer.AdjustPrepayment(495000);

        group.UpdateOpenStatus(m_Now, new Dictionary<Guid, long>());
        group.UpdateOpenStatus(m_Now);

        Assert.Equal(85000, group.SettlementPriceKopecks);
        Assert.Equal(425000, buyer.Order?.PaidKopecks);
        Assert.Equal(70000, buyer.LimitRefundsKopecks);
        Assert.Equal(5, group.EligibleQuantity(85000));
    }

    [Fact]
    public void QuoteReplacesTheBuyersExistingLimitWithoutCountingThemTwice() {
        var group = CreateGroup();
        var buyer = group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        group.AddParticipant(Guid.NewGuid(), m_Now, 99000);

        Assert.Null(group.FindReachableTier(buyer.UserId, 99000));
        Assert.Equal(99000, group.FindReachableTier(Guid.NewGuid(), 99000)?.PriceKopecks);
    }

    [Fact]
    public void SettlementRefundsAnEarlierDirectPaymentAtTheCheapestReachableTier() {
        var group = CreateGroup();
        var organizer = group.AddParticipant(group.OrganizerId, m_Now, 85000);
        var direct = group.AddParticipant(Guid.NewGuid(), m_Now);
        direct.RecordOrder(99000, m_Now);

        for (var i = 0; i < 3; i++) {
            group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        }

        group.UpdateOpenStatus(m_Now);
        var order = organizer.Order;
        group.UpdateOpenStatus(group.Deadline);

        Assert.Equal(EGroupPurchaseStatus.Successful, group.Status);
        Assert.Equal(85000, group.SettlementPriceKopecks);
        Assert.Same(order, organizer.Order);
        Assert.Equal(85000, organizer.Order?.PaidKopecks);
        Assert.Equal(99000, direct.Order?.PaidKopecks);
        Assert.Equal(14000, direct.Order?.PriceRefundKopecks);
        Assert.Null(direct.Order?.RefundedAt);
        Assert.Equal(m_Now, group.BonusAwardedAt);
    }

    [Theory]
    [InlineData("2", 198000, 28000)]
    [InlineData("2.125", 210375, 29750)]
    [InlineData("0.001", 99, 14)]
    public void DirectPriceRefundUsesPurchasedQuantityAndIsIdempotent(string amount, long paid, long refund) {
        var quantity = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var group = CreateGroup();
        var direct = group.AddParticipant(Guid.NewGuid(), m_Now);
        direct.Quantity = quantity;
        direct.RecordOrder(99000, m_Now);
        group.AddParticipant(Guid.NewGuid(), m_Now, 85000).Quantity = 5;

        group.UpdateOpenStatus(m_Now);
        group.UpdateOpenStatus(m_Now.AddMinutes(1));

        Assert.Equal(85000, group.SettlementPriceKopecks);
        Assert.Equal(paid, direct.Order?.PaidKopecks);
        Assert.Equal(refund, direct.Order?.PriceRefundKopecks);
        Assert.Equal(0, direct.LimitRefundsKopecks);
    }

    [Fact]
    public void OrganizerEarnsTheGiftEvenWhenTheirLimitIsTooLow() {
        var group = CreateGroup();
        var organizer = group.AddParticipant(group.OrganizerId, m_Now, 85000);
        var revoked = group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        revoked.LimitRevoked = true;

        for (var i = 0; i < 2; i++) {
            group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        }
        group.UpdateOpenStatus(m_Now);
        Assert.Equal(EGroupPurchaseStatus.Open, group.Status);

        group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        group.UpdateOpenStatus(m_Now);

        Assert.Null(organizer.Order);
        Assert.Null(revoked.Order);
        Assert.Equal(5m, group.BonusQuantity);
        Assert.Equal("кг", group.BonusUnit);
        Assert.Equal(m_Now, group.BonusAwardedAt);
    }

    [Fact]
    public void DeadlineWinsOverReachableTierAndRefundsOnlyOnce() {
        var group = CreateGroup();
        var direct = group.AddParticipant(Guid.NewGuid(), m_Now);
        direct.RecordOrder(99000, m_Now);
        var pending = group.AddParticipant(Guid.NewGuid(), m_Now, 99000);
        group.AddParticipant(group.OrganizerId, m_Now, 99000);

        group.UpdateOpenStatus(group.Deadline);
        group.UpdateOpenStatus(group.Deadline.AddHours(1));

        Assert.Equal(EGroupPurchaseStatus.Cancelled, group.Status);
        Assert.Equal(group.Deadline, direct.Order?.RefundedAt);
        Assert.Null(pending.Order);
        Assert.Null(group.BonusAwardedAt);
    }
}
