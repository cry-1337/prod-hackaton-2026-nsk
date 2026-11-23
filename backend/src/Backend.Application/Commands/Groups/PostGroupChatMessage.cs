using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Groups;

public sealed record PostGroupChatMessageCommand(string Identifier, Guid UserId, string Text, string? IdempotencyKey)
    : ICommand<GroupChatMessageDto>;

public sealed class PostGroupChatMessageValidator : AbstractValidator<PostGroupChatMessageCommand> {
    public PostGroupChatMessageValidator() {
        RuleFor(command => command.Text).NotEmpty().WithErrorCode("CHAT_TEXT_REQUIRED")
            .MaximumLength(1000).WithErrorCode("CHAT_TEXT_TOO_LONG")
            .Must(text => text is null || !text.Contains('\0')).WithErrorCode("CHAT_TEXT_INVALID");
        RuleFor(command => command.IdempotencyKey).Cascade(CascadeMode.Stop).NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_EMPTY")
            .MaximumLength(128).WithErrorCode("IDEMPOTENCY_KEY_TOO_LONG")
            .Must(key => !key!.Contains('`') && !key.Contains('\0')).WithErrorCode("IDEMPOTENCY_KEY_INVALID")
            .When(command => command.IdempotencyKey is not null);
    }
}

public sealed class PostGroupChatMessageHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<PostGroupChatMessageCommand, GroupChatMessageDto> {
    public async ValueTask<GroupChatMessageDto> Handle(
        PostGroupChatMessageCommand command,
        CancellationToken cancellationToken
    ) {
        var groupId = await dbContext.GroupPurchases.ByIdentifier(command.Identifier).Select(group => (Guid?)group.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockGroupPurchaseAsync(groupId, cancellationToken);
        await dbContext.RequireAccessAsync(groupId, command.UserId, cancellationToken);

        if (command.IdempotencyKey is { } key) {
            var previous = await dbContext.GroupChatMessages.AsNoTracking()
                .Where(message => message.GroupPurchaseId == groupId && message.AuthorId == command.UserId &&
                    message.IdempotencyKey == key)
                .ToDtos().SingleOrDefaultAsync(cancellationToken);
            if (previous is not null) {
                return previous.Text == command.Text ? previous : throw new OperationConflictException("IDEMPOTENCY_KEY_REUSED");
            }
        }

        var nickname = await dbContext.Users.Where(user => user.Id == command.UserId).Select(user => user.Nickname)
            .SingleAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = new GroupChatMessageEntity {
            GroupPurchaseId = groupId,
            AuthorId = command.UserId,
            Text = command.Text,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero)
        };

        dbContext.GroupChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GroupChatMessageDto(message.Id, message.AuthorId, nickname, message.Text, message.CreatedAt);
    }
}
