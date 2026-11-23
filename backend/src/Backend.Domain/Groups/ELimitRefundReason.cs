namespace Backend.Domain.Groups;

public enum ELimitRefundReason {
    LimitChanged = 1,
    LimitRevoked = 2,
    GroupCancelled = 3,
    PriceDrop = 4,
    Unmatched = 5,
    DirectCheckout = 6
}
