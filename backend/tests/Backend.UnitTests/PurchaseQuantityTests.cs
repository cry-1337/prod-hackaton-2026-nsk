using Backend.Domain.Catalog;
using Backend.Domain.Groups;
using Xunit;

namespace Backend.UnitTests;

public sealed class PurchaseQuantityTests {
    [Theory]
    [InlineData(0, ESaleUnit.Piece, false)]
    [InlineData(-1, ESaleUnit.Kilogram, false)]
    [InlineData(2, ESaleUnit.Piece, true)]
    [InlineData(1.5, ESaleUnit.Piece, false)]
    [InlineData(0.001, ESaleUnit.Kilogram, true)]
    [InlineData(0.0001, ESaleUnit.Kilogram, false)]
    [InlineData(1000000000, ESaleUnit.Piece, false)]
    public void ValidatesQuantityForProductUnit(decimal quantity, ESaleUnit unit, bool expected) {
        Assert.Equal(expected, PurchaseQuantity.IsValid(quantity, unit, 15900));
    }

    [Fact]
    public void RejectsUnrepresentableOrderTotal() {
        Assert.False(PurchaseQuantity.IsValid(2, ESaleUnit.Piece, long.MaxValue));
    }

    [Theory]
    [InlineData(15900, 2.125, 33788)]
    [InlineData(15900, 0.001, 16)]
    [InlineData(99000, 2, 198000)]
    [InlineData(1, 0.001, 1)]
    [InlineData(0, 2, 0)]
    public void RoundsOnceForTheWholeOrder(long price, decimal quantity, long expected) {
        Assert.Equal(expected, PurchaseQuantity.Total(price, quantity));
    }
}
