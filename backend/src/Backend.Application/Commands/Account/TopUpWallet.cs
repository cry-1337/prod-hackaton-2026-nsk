using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Users;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Account;

public sealed record TopUpWalletCommand(Guid UserId, long AmountKopecks, string? IdempotencyKey) : ICommand<WalletTopUpDto>;

public sealed class TopUpWalletValidator : AbstractValidator<TopUpWalletCommand> {
    public TopUpWalletValidator() {
        RuleFor(command => command.AmountKopecks).InclusiveBetween(1, 100_000_000).WithErrorCode("TOP_UP_AMOUNT_INVALID");
        RuleFor(command => command.IdempotencyKey).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_REQUIRED")
            .MaximumLength(128).WithErrorCode("IDEMPOTENCY_KEY_TOO_LONG")
            .Must(key => !key!.Contains('\0')).WithErrorCode("IDEMPOTENCY_KEY_INVALID");
    }
}

public sealed class TopUpWalletHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<TopUpWalletCommand, WalletTopUpDto> {
    public async ValueTask<WalletTopUpDto> Handle(TopUpWalletCommand command, CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockUsersAsync([command.UserId], cancellationToken);
        if (!await dbContext.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken)) {
            throw new InvalidCredentialsException();
        }

        var topUp = await dbContext.WalletTopUps.SingleOrDefaultAsync(entry =>
            entry.UserId == command.UserId && entry.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (topUp is null) {
            var now = clock.GetUtcNow();
            topUp = new WalletTopUpEntity {
                UserId = command.UserId,
                AmountKopecks = command.AmountKopecks,
                IdempotencyKey = command.IdempotencyKey!,
                CreatedAt = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero)
            };
            dbContext.WalletTopUps.Add(topUp);
            await dbContext.SaveChangesAsync(cancellationToken);
        } else if (topUp.AmountKopecks != command.AmountKopecks) {
            throw new OperationConflictException("IDEMPOTENCY_KEY_REUSED");
        }

        await transaction.CommitAsync(cancellationToken);
        return new WalletTopUpDto(topUp.Id, topUp.AmountKopecks, topUp.CreatedAt);
    }
}
