namespace Backend.Application.Dto;

public sealed record OrganizerProfileDto(Guid Id, string Nickname, int OpenGroupsCount, int SuccessfulGroupsCount) {
    public int LikesCount { get; init; }
    public bool LikedByMe { get; init; }
}
