using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

internal static class GroupChatReadModel {
    public static async Task RequireAccessAsync(
        this IAppDbContext dbContext,
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken
    ) {
        var canAccess = await dbContext.GroupPurchases.Where(group => group.Id == groupId)
            .Select(group => (bool?)(group.OrganizerId == userId ||
                group.Participants.Any(participant => participant.UserId == userId && !participant.LimitRevoked)))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");

        if (!canAccess) {
            throw new AccessDeniedException("GROUP_CHAT_FORBIDDEN");
        }
    }

    public static IQueryable<GroupChatMessageDto> ToDtos(this IQueryable<GroupChatMessageEntity> messages) =>
        messages.Select(message => new GroupChatMessageDto(message.Id, message.AuthorId, message.Author.Nickname,
            message.Text, message.CreatedAt));
}
