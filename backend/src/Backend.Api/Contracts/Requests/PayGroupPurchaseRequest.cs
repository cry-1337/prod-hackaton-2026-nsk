using System.Text.Json.Serialization;

namespace Backend.Api.Contracts.Requests;

public sealed record PayGroupPurchaseRequest([property: JsonRequired] long ConfirmedPriceKopecks, Guid? CardId, DeliveryAddressRequest Address) {
    public decimal Quantity { get; init; } = 1m;
}
