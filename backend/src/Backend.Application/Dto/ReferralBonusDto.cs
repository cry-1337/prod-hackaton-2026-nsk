namespace Backend.Application.Dto;

public sealed record ReferralBonusDto(Guid FriendId, string FriendNickname, string GroupId, long AmountKopecks, DateTimeOffset CreatedAt);
