namespace Backend.Application.Dto;

public sealed record GroupChatMessageDto(long Id, Guid AuthorId, string AuthorNickname, string Text, DateTimeOffset CreatedAt);
