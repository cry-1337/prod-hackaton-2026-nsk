using System.Text.Json;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Validation;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Groups;

public sealed record ChangeGroupLimitCommand(string Identifier, Guid UserId, long? LimitKopecks, string? IdempotencyKey)
    : ICommand<GroupLimitDto> {
    public decimal Quantity { get; init; } = 1m;

    public Guid? CardId { get; init; }
    public DeliveryAddress? Address { get; init; }
}

public sealed class ChangeGroupLimitValidator : AbstractValidator<ChangeGroupLimitCommand> {
    public ChangeGroupLimitValidator() {
        RuleFor(command => command.LimitKopecks).GreaterThan(0).WithErrorCode("LIMIT_MUST_BE_POSITIVE");
        RuleFor(command => command.Address).NotNull().WithErrorCode("DELIVERY_ADDRESS_REQUIRED")
            .SetValidator(new DeliveryAddressValidator()!).When(command => command.LimitKopecks.HasValue);
        RuleFor(command => command.IdempotencyKey).NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_EMPTY")
            .MaximumLength(128).WithErrorCode("IDEMPOTENCY_KEY_TOO_LONG")
            .When(command => command.IdempotencyKey is not null);
    }
}

public sealed class ChangeGroupLimitHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<ChangeGroupLimitCommand, GroupLimitDto> {
    public async ValueTask<GroupLimitDto> Handle(ChangeGroupLimitCommand command, CancellationToken cancellationToken) {
        var groupId = await dbContext.GroupPurchases.ByIdentifier(command.Identifier).Select(group => (Guid?)group.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
        if (!await dbContext.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken)) {
            throw new InvalidCredentialsException();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockGroupPurchaseAsync(groupId, cancellationToken);

        var key = command.IdempotencyKey;
        var address = command.Address?.ToStorage();
        if (key is not null) {
            var previous = await dbContext.GroupLimitOperations.FindAsync([groupId, command.UserId, key], cancellationToken);
            if (previous is not null) {
                if (previous.Quantity != command.Quantity || previous.LimitKopecks != command.LimitKopecks || previous.CardId != command.CardId ||
                    previous.DeliveryAddress != address) {
                    throw new OperationConflictException("IDEMPOTENCY_KEY_REUSED");
                }

                return JsonSerializer.Deserialize<GroupLimitDto>(previous.ResultJson)!;
            }
        }

        var group = await dbContext.GroupPurchases.WithDetails(QueryTrackingBehavior.TrackAll)
            .SingleAsync(group => group.Id == groupId, cancellationToken);
        await dbContext.LockUsersAsync(group.Participants.Select(item => item.UserId).Append(command.UserId).Distinct().ToArray(), cancellationToken);
        if (command.LimitKopecks is not null) {
            await dbContext.EnsureActiveCardAsync(command.UserId, command.CardId, cancellationToken);
        }

        var now = clock.GetUtcNow();
        if (group.Status == EGroupPurchaseStatus.Open && group.Deadline <= now) {
            group.UpdateOpenStatus(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (group.Status != EGroupPurchaseStatus.Open) {
            throw new OperationConflictException("GROUP_CLOSED");
        }

        var participant = group.Participants.SingleOrDefault(item => item.UserId == command.UserId);
        if (participant is { LimitKopecks: null }) {
            throw new OperationConflictException("ALREADY_JOINED");
        }

        var balances = await dbContext.GetGroupWalletBalancesAsync([group], command.UserId, cancellationToken);
        if (command.LimitKopecks is { } limit) {
            GroupCheckout.ValidateLimitPrice(group, limit);
            GroupCheckout.ValidateQuantity(command.Quantity, group.SaleUnit, Math.Max(group.RegularPriceKopecks, group.StartingPriceKopecks));
            var advance = PurchaseQuantity.Total(limit, command.Quantity);
            if (advance - (participant?.NetPrepayment ?? 0) > balances.GetValueOrDefault(command.UserId)) {
                throw new OperationConflictException("INSUFFICIENT_BALANCE");
            }

            if (participant is null) {
                participant = new GroupPurchaseParticipantEntity { GroupPurchaseId = group.Id, UserId = command.UserId, JoinedAt = now };
                group.Participants.Add(participant);
                dbContext.GroupPurchaseParticipants.Add(participant);
            }

            participant.AdjustPrepayment(advance);
            participant.Quantity = command.Quantity;
            participant.UsesWallet = true;
            participant.LimitKopecks = limit;
            participant.LimitRevoked = false;
            participant.CardId = command.CardId ?? participant.CardId;
            participant.DeliveryAddress = address;

            group.UpdateOpenStatus(now, balances);
        } else {
            if (participant is null) {
                throw new EntityNotFoundException("LIMIT_NOT_FOUND");
            }
            participant.LimitRevoked = true;
            participant.AdjustPrepayment(0, ELimitRefundReason.LimitRevoked);
        }

        var result = group.GetLimit(command.UserId, now, balances)!;
        if (key is not null) {
            dbContext.GroupLimitOperations.Add(new GroupLimitOperationEntity {
                GroupPurchaseId = group.Id, UserId = command.UserId, Key = key,
                Quantity = command.Quantity, LimitKopecks = command.LimitKopecks, CardId = command.CardId, DeliveryAddress = address,
                ResultJson = JsonSerializer.Serialize(result)
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
