namespace Backend.Api.Contracts.Responses;

public sealed record ReferralBonusResponse(Guid FriendId, string FriendNickname, string GroupId, long AmountKopecks, DateTimeOffset CreatedAt);
