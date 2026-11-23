namespace Backend.Application.Dto;

public sealed record GroupParticipantDto(Guid UserId, string Nickname, long? UnitPriceKopecks, bool IsLimit);
