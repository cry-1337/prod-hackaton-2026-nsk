using Backend.Domain.Groups;

namespace Backend.Api.Contracts.Requests;

public sealed record DeliveryAddressRequest(string City, string Street, string House, string? Building = null, string? Apartment = null) {
    public DeliveryAddress ToAddress() => new(City.Trim(), Street.Trim(), House.Trim(), Optional(Building), Optional(Apartment));

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
