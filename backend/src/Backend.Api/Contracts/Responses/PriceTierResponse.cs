using System.Text.Json.Serialization;

namespace Backend.Api.Contracts.Responses;

public sealed record PriceTierResponse(int RequiredParticipants, long PriceKopecks) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WaitingBuyers { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? EligibleQuantity { get; init; }

    public int RequiredQuantity => RequiredParticipants;
}
