namespace Backend.Api.Contracts.Responses;

public sealed record OrganizerProfileResponse(Guid Id, string Nickname, int OpenGroupsCount, int SuccessfulGroupsCount) {
    public int LikesCount { get; init; }
    public bool LikedByMe { get; init; }
}
