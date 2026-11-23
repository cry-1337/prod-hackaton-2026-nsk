using Backend.Domain.Catalog;

namespace Backend.Domain.Groups;

public static class PurchaseQuantity {
    public static bool IsValid(decimal quantity, ESaleUnit unit, long maxUnitPrice) =>
        quantity > 0 && quantity <= 999999999.999m &&
        quantity == decimal.Round(quantity, unit == ESaleUnit.Kilogram ? 3 : 0) &&
        quantity * maxUnitPrice <= long.MaxValue;

    public static long Total(long unitPrice, decimal quantity) =>
        unitPrice == 0 ? 0 : Math.Max(1L, (long)decimal.Round(unitPrice * quantity, 0, MidpointRounding.AwayFromZero));
}
