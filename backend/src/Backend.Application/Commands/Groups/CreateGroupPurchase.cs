using System.Security.Cryptography;
using System.Text.Json;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Validation;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Groups;

public sealed record CreateGroupPurchaseCommand(
    Guid ProductId,
    DateTimeOffset Deadline,
    EGroupPurchaseReward? RewardKind = null,
    int? TargetParticipants = null,
    long? OrganizerLimitKopecks = null
) : ICommand<GroupPurchaseDto> {
    public Guid OrganizerId { get; init; }
    public string? CreationKey { get; init; }
    public decimal Quantity { get; init; } = 1m;

    public Guid? CardId { get; init; }
    public DeliveryAddress? Address { get; init; }
    public long? ConfirmedPriceKopecks { get; init; }
}

public sealed class CreateGroupPurchaseCommandValidator : AbstractValidator<CreateGroupPurchaseCommand> {
    public CreateGroupPurchaseCommandValidator() {
        RuleFor(command => command.ProductId).NotEmpty().WithErrorCode("PRODUCT_REQUIRED");
        RuleFor(command => command.RewardKind).IsInEnum().WithErrorCode("REWARD_KIND_INVALID");
        RuleFor(command => command.Address).NotNull().WithErrorCode("DELIVERY_ADDRESS_REQUIRED")
            .SetValidator(new DeliveryAddressValidator()!);
        RuleFor(command => command.ConfirmedPriceKopecks).GreaterThanOrEqualTo(0).WithErrorCode("PRICE_MUST_BE_NON_NEGATIVE");
        RuleFor(command => command.CreationKey)
            .NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_EMPTY")
            .MaximumLength(128).WithErrorCode("IDEMPOTENCY_KEY_TOO_LONG")
            .When(command => command.CreationKey is not null);
    }
}

public sealed class CreateGroupPurchaseHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<CreateGroupPurchaseCommand, GroupPurchaseDto> {
    public async ValueTask<GroupPurchaseDto> Handle(
        CreateGroupPurchaseCommand command,
        CancellationToken cancellationToken
    ) {
        var deadline = command.Deadline.ToUniversalTime();
        deadline = new DateTimeOffset(deadline.Ticks - deadline.Ticks % 10, TimeSpan.Zero);
        var key = command.CreationKey;

        if (key is not null && await FindExistingAsync(command.OrganizerId, key, cancellationToken) is { } existing) {
            return GetCreationResult(existing, command, deadline);
        }

        var hasTierSelection = command.OrganizerLimitKopecks.HasValue || command.TargetParticipants.HasValue;
        if (command.RewardKind == EGroupPurchaseReward.Cashback) {
            throw InvalidField(nameof(command.RewardKind), "REWARD_KIND_INVALID");
        }

        if (!hasTierSelection) {
            throw InvalidField(nameof(command.OrganizerLimitKopecks), "PRICE_TIER_REQUIRED");
        }

        var product = await dbContext.Products.AsNoTracking()
            .Include(product => product.PriceTiers)
            .SingleOrDefaultAsync(product => product.Id == command.ProductId, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND");

        if (product.PriceTiers.Count == 0) {
            throw new OperationConflictException("PRODUCT_HAS_NO_GROUP_PRICES");
        }

        var selected = product.PriceTiers.FirstOrDefault(tier =>
            (command.TargetParticipants == null || tier.RequiredParticipants == command.TargetParticipants) &&
            (command.OrganizerLimitKopecks == null || tier.PriceKopecks == command.OrganizerLimitKopecks));
        if (selected is null) {
            var field = command.OrganizerLimitKopecks.HasValue ? nameof(command.OrganizerLimitKopecks) : nameof(command.TargetParticipants);
            throw InvalidField(field, "PRICE_TIER_UNAVAILABLE");
        }

        GroupCheckout.ValidateQuantity(command.Quantity, product.SaleUnit, Math.Max(product.RegularPriceKopecks, product.PriceTiers.Max(tier => tier.PriceKopecks)));

        var organizer = await dbContext.Users.AsNoTracking()
            .Include(user => user.OrganizerLikes)
            .SingleOrDefaultAsync(user => user.Id == command.OrganizerId, cancellationToken)
            ?? throw new InvalidCredentialsException();

        await dbContext.EnsureActiveCardAsync(organizer.Id, command.CardId, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockUsersAsync([organizer.Id], cancellationToken);
        if (key is not null && await FindExistingAsync(organizer.Id, key, cancellationToken) is { } concurrent) {
            return GetCreationResult(concurrent, command, deadline);
        }

        var now = clock.GetUtcNow();
        if (deadline <= now) {
            throw InvalidField(nameof(command.Deadline), "DEADLINE_MUST_BE_IN_FUTURE");
        }

        var balances = await dbContext.GetGroupWalletBalancesAsync([], organizer.Id, cancellationToken);
        var organizerBalance = balances.GetValueOrDefault(organizer.Id);
        var advance = PurchaseQuantity.Total(selected.PriceKopecks, command.Quantity);
        if (advance > organizerBalance) {
            throw new OperationConflictException("INSUFFICIENT_BALANCE");
        }

        var group = new GroupPurchaseEntity {
            ProductId = product.Id,
            OrganizerId = organizer.Id,
            CreationKey = key,
            CreationCheckoutHash = key is null ? null : HashCheckout(command),
            ConfirmedCreationPriceKopecks = command.ConfirmedPriceKopecks,
            Deadline = deadline,
            CreatedAt = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero),
            RegularPriceKopecks = product.RegularPriceKopecks,
            SaleUnit = product.SaleUnit,
            OrganizerInitialLimitKopecks = selected.PriceKopecks,
            OrganizerInitialQuantity = command.Quantity,
            RewardKind = EGroupPurchaseReward.FreeProduct,
            BonusQuantity = product.BonusQuantity,
            BonusUnit = product.BonusUnit,
            OrganizerWalletBalanceAtCreationKopecks = organizerBalance,
        };

        foreach (var tier in product.PriceTiers) {
            group.PriceTiers.Add(new GroupPurchasePriceTierEntity {
                GroupPurchaseId = group.Id,
                RequiredParticipants = tier.RequiredParticipants,
                PriceKopecks = tier.PriceKopecks
            });
        }

        var participant = group.AddParticipant(organizer.Id, group.CreatedAt, group.OrganizerInitialLimitKopecks);
        participant.AdjustPrepayment(advance);
        participant.Quantity = command.Quantity;
        participant.DeliveryAddress = command.Address!.ToStorage();
        participant.CardId = command.CardId;
        participant.UsesWallet = true;

        group.UpdateOpenStatus(group.CreatedAt, balances);

        await dbContext.AddGroupPurchaseAsync(group, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return group.ToDto(product, organizer, group.CreatedAt, balances);
    }

    private static string HashCheckout(CreateGroupPurchaseCommand command) {
        var checkout = JsonSerializer.SerializeToUtf8Bytes(new { command.CardId, DeliveryAddress = command.Address!.ToStorage() });
        return Convert.ToHexString(SHA256.HashData(checkout));
    }

    private static ValidationException InvalidField(string field, string code) =>
        new([new ValidationFailure(field, code) { ErrorCode = code }]);

    private Task<GroupPurchaseEntity?> FindExistingAsync(Guid organizerId, string creationKey, CancellationToken cancellationToken) =>
        dbContext.GroupPurchases.WithDetails().SingleOrDefaultAsync(
            group => group.OrganizerId == organizerId && group.CreationKey == creationKey,
            cancellationToken);

    private static GroupPurchaseDto GetCreationResult(
        GroupPurchaseEntity group,
        CreateGroupPurchaseCommand command,
        DateTimeOffset deadline
    ) {
        var requestedLimit = command.OrganizerLimitKopecks ?? group.PriceTiers
            .FirstOrDefault(tier => tier.RequiredParticipants == command.TargetParticipants)?.PriceKopecks;

        var sameSelection = group.OrganizerInitialLimitKopecks.HasValue
            ? group.OrganizerInitialLimitKopecks == requestedLimit &&
              (command.TargetParticipants == null || group.PriceTiers.Any(tier =>
                  tier.RequiredParticipants == command.TargetParticipants && tier.PriceKopecks == requestedLimit))
            : command.OrganizerLimitKopecks == null && group.TargetParticipants == command.TargetParticipants &&
              (group.TargetParticipants.HasValue || command.RewardKind.HasValue);

        if (group.OrganizerInitialQuantity != command.Quantity || group.ProductId != command.ProductId || group.Deadline != deadline || !sameSelection ||
            group.RewardKind != (command.RewardKind ?? EGroupPurchaseReward.FreeProduct) ||
            (group.CreationCheckoutHash is { } hash && hash != HashCheckout(command)) ||
            group.ConfirmedCreationPriceKopecks != command.ConfirmedPriceKopecks) {
            throw new OperationConflictException("IDEMPOTENCY_KEY_REUSED");
        }

        var original = new GroupPurchaseEntity {
            Id = group.Id,
            ProductId = group.ProductId,
            OrganizerId = group.OrganizerId,
            InviteCode = group.InviteCode,
            CreatedAt = group.CreatedAt,
            Deadline = group.Deadline,
            RegularPriceKopecks = group.RegularPriceKopecks,
            SaleUnit = group.SaleUnit,
            TargetParticipants = group.TargetParticipants,
            OrganizerInitialLimitKopecks = group.OrganizerInitialLimitKopecks,
            BonusQuantity = group.BonusQuantity,
            BonusUnit = group.BonusUnit,
            RewardKind = group.RewardKind
        };

        foreach (var tier in group.PriceTiers) {
            original.PriceTiers.Add(tier);
        }

        var participant = original.AddParticipant(group.OrganizerId, group.CreatedAt, group.OrganizerInitialLimitKopecks);
        participant.Quantity = group.OrganizerInitialQuantity;
        var balances = new Dictionary<Guid, long>();
        if (group.OrganizerWalletBalanceAtCreationKopecks is { } organizerBalance) {
            participant.UsesWallet = true;
            balances.Add(group.OrganizerId, organizerBalance);
        }

        original.UpdateOpenStatus(group.CreatedAt, balances);
        return original.ToDto(group.Product, group.Organizer, group.CreatedAt, balances);
    }
}
