namespace Backend.Api.Contracts.Responses;

public sealed record GroupChatMessageResponse(long Id, Guid AuthorId, string AuthorNickname, string Text, DateTimeOffset CreatedAt);
