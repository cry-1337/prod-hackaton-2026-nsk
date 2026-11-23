using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupChatMessagesQuery(string Identifier, long? BeforeId, long? AfterId, int Limit)
    : IQuery<IReadOnlyList<GroupChatMessageDto>>;

public sealed class GetGroupChatMessagesValidator : AbstractValidator<GetGroupChatMessagesQuery> {
    public GetGroupChatMessagesValidator() {
        RuleFor(query => query.BeforeId).GreaterThan(0).WithErrorCode("CHAT_CURSOR_INVALID");
        RuleFor(query => query.AfterId).GreaterThanOrEqualTo(0).WithErrorCode("CHAT_CURSOR_INVALID");
        RuleFor(query => query.AfterId).Null().When(query => query.BeforeId.HasValue).WithErrorCode("CHAT_CURSORS_CONFLICT");
        RuleFor(query => query.Limit).InclusiveBetween(1, 100).WithErrorCode("CHAT_PAGE_SIZE_INVALID");
    }
}

public sealed class GetGroupChatMessagesHandler(IAppDbContext dbContext)
    : IQueryHandler<GetGroupChatMessagesQuery, IReadOnlyList<GroupChatMessageDto>> {
    public async ValueTask<IReadOnlyList<GroupChatMessageDto>> Handle(
        GetGroupChatMessagesQuery query,
        CancellationToken cancellationToken
    ) {
        var groupId = await dbContext.GroupPurchases.ByIdentifier(query.Identifier).Select(group => (Guid?)group.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");

        var messages = dbContext.GroupChatMessages.AsNoTracking().Where(message => message.GroupPurchaseId == groupId);
        if (query.AfterId is { } afterId) {
            return await messages.Where(message => message.Id > afterId).OrderBy(message => message.Id)
                .Take(query.Limit).ToDtos().ToArrayAsync(cancellationToken);
        }

        if (query.BeforeId is { } beforeId) {
            messages = messages.Where(message => message.Id < beforeId);
        }

        var history = await messages.OrderByDescending(message => message.Id).Take(query.Limit)
            .ToDtos().ToArrayAsync(cancellationToken);
        Array.Reverse(history);
        return history;
    }
}
