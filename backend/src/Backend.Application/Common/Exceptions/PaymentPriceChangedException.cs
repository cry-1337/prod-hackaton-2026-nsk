using Backend.Application.Dto;

namespace Backend.Application.Common.Exceptions;

public sealed class PaymentPriceChangedException(GroupPaymentQuoteDto quote) : Exception("PRICE_CONFIRMATION_REQUIRED") {
    public GroupPaymentQuoteDto Quote { get; } = quote;
}
