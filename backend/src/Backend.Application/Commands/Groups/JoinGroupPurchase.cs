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

public sealed record JoinGroupPurchaseCommand(string Identifier, Guid UserId) : ICommand<GroupPurchaseDto> {
    public long? ConfirmedPriceKopecks { get; init; }
    public decimal Quantity { get; init; } = 1m;

    public Guid? CardId { get; init; }
    public DeliveryAddress? Address { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed class JoinGroupPurchaseCommandValidator : AbstractValidator<JoinGroupPurchaseCommand> {
    public JoinGroupPurchaseCommandValidator() {
        When(command => command.ConfirmedPriceKopecks is not null, () => {
            RuleFor(command => command.ConfirmedPriceKopecks).GreaterThanOrEqualTo(0).WithErrorCode("PRICE_MUST_BE_NON_NEGATIVE");
        });
        RuleFor(command => command.Address).NotNull().WithErrorCode("DELIVERY_ADDRESS_REQUIRED")
            .SetValidator(new DeliveryAddressValidator()!);
        RuleFor(command => command.IdempotencyKey).NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_EMPTY")
            .MaximumLength(128).WithErrorCode("IDEMPOTENCY_KEY_TOO_LONG")
            .When(command => command.IdempotencyKey is not null);
    }
}

public sealed class JoinGroupPurchaseHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<JoinGroupPurchaseCommand, GroupPurchaseDto> {
    public async ValueTask<GroupPurchaseDto> Handle(JoinGroupPurchaseCommand command, CancellationToken cancellationToken) {
        var groupId = await dbContext.GroupPurchases.ByIdentifier(command.Identifier)
            .Select(group => (Guid?)group.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");

        if (!await dbContext.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken)) {
            throw new InvalidCredentialsException();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockGroupPurchaseAsync(groupId, cancellationToken);
        var key = command.IdempotencyKey;
        var address = command.Address!.ToStorage();
        if (key is not null) {
            var previous = await dbContext.GroupPaymentOperations.FindAsync([groupId, command.UserId, key], cancellationToken);
            if (previous is not null) {
                if (previous.Quantity != command.Quantity || previous.ConfirmedPriceKopecks != command.ConfirmedPriceKopecks || previous.CardId != command.CardId ||
                    previous.DeliveryAddress != address) {
                    throw new OperationConflictException("IDEMPOTENCY_KEY_REUSED");
                }

                return JsonSerializer.Deserialize<GroupPurchaseDto>(previous.ResultJson)!;
            }
        }

        var group = await dbContext.GroupPurchases.WithDetails(QueryTrackingBehavior.TrackAll)
            .SingleAsync(group => group.Id == groupId, cancellationToken);
        var participant = group.Participants.SingleOrDefault(item => item.UserId == command.UserId);
        await dbContext.EnsureActiveCardAsync(command.UserId, command.ConfirmedPriceKopecks is null ? null : command.CardId, cancellationToken);
        await dbContext.LockUsersAsync(group.Participants.Select(item => item.UserId).Append(command.UserId).Distinct().ToArray(), cancellationToken);
        var now = clock.GetUtcNow();
        if (group.Status == EGroupPurchaseStatus.Open && group.Deadline <= now) {
            group.UpdateOpenStatus(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (participant is not null && (participant.LimitKopecks is null || participant.Order is not null)) {
            if (command.ConfirmedPriceKopecks is null) {
                var currentBalances = await dbContext.GetGroupWalletBalancesAsync([group], null, cancellationToken);
                return group.ToDto(now, currentBalances);
            }

            throw new OperationConflictException("ALREADY_JOINED");
        }

        if (group.Status != EGroupPurchaseStatus.Open) {
            throw new OperationConflictException("GROUP_CLOSED");
        }

        var balances = await dbContext.GetGroupWalletBalancesAsync([group], command.UserId, cancellationToken);
        var quote = group.QuotePayment(command.UserId, balances, command.Quantity);
        if (command.ConfirmedPriceKopecks is { } confirmed && quote.PriceKopecks > confirmed) {
            throw new PaymentPriceChangedException(quote);
        }

        if (quote.TotalKopecks - (participant?.NetPrepayment ?? 0) > balances.GetValueOrDefault(command.UserId)) {
            throw new OperationConflictException("INSUFFICIENT_BALANCE");
        }
        if (participant is null) {
            participant = group.AddParticipant(command.UserId, now);
            dbContext.GroupPurchaseParticipants.Add(participant);
        }

        participant.AdjustPrepayment(0, ELimitRefundReason.DirectCheckout);
        participant.Quantity = command.Quantity;
        participant.UsesWallet = true;
        participant.CardId = command.CardId ?? participant.CardId;
        participant.DeliveryAddress = address;
        participant.RecordOrder(quote.PriceKopecks, now);
        group.ConfirmDirectParticipation(participant, now, balances);

        var result = group.ToDto(now, balances);
        if (command.ConfirmedPriceKopecks is { } price) {
            result = result with { MyPayment = group.GetPayment(command.UserId) };
            if (key is not null) {
                dbContext.GroupPaymentOperations.Add(new GroupPaymentOperationEntity {
                    GroupPurchaseId = groupId,
                    UserId = command.UserId,
                    Key = key,
                    Quantity = command.Quantity,
                    ConfirmedPriceKopecks = price,
                    CardId = command.CardId,
                    DeliveryAddress = address,
                    ResultJson = JsonSerializer.Serialize(result)
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
