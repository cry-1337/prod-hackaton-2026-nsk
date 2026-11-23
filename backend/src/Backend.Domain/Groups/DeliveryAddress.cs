using System.Text.Json;

namespace Backend.Domain.Groups;

public sealed record DeliveryAddress(string City, string Street, string House, string? Building = null, string? Apartment = null) {
    public string ToStorage() => JsonSerializer.Serialize(this);

    public string ToLabel() => string.Join(", ", new[] {
        City, Street, "д. " + House, Building is null ? null : "корп. " + Building, Apartment is null ? null : "кв. " + Apartment
    }.Where(part => part is not null));

    public static DeliveryAddress? FromStorage(string? value) {
        if (value is null || !value.StartsWith('{')) {
            return null;
        }

        try {
            var address = JsonSerializer.Deserialize<DeliveryAddress>(value);
            return address is { City.Length: > 0, Street.Length: > 0, House.Length: > 0 } ? address : null;
        } catch (JsonException) {
            return null;
        }
    }
}
