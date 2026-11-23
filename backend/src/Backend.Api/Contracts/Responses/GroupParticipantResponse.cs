namespace Backend.Api.Contracts.Responses;

public sealed record GroupParticipantResponse(Guid UserId, string Nickname, long? UnitPriceKopecks, bool IsLimit);
