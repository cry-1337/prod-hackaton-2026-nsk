namespace Backend.Application.Dto;

public sealed record PriceTierDto(int RequiredParticipants, long PriceKopecks) {
    public int? WaitingBuyers { get; init; }
    public decimal? EligibleQuantity { get; init; }
}
