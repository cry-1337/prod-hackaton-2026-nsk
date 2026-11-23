using System.Security.Cryptography;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Catalog;
using Backend.Domain.Groups;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Infrastructure.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext {
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    public DbSet<ProductPriceTierEntity> ProductPriceTiers => Set<ProductPriceTierEntity>();

    public DbSet<GroupPurchaseEntity> GroupPurchases => Set<GroupPurchaseEntity>();

    public DbSet<GroupPurchasePriceTierEntity> GroupPurchasePriceTiers => Set<GroupPurchasePriceTierEntity>();

    public DbSet<GroupPurchaseParticipantEntity> GroupPurchaseParticipants => Set<GroupPurchaseParticipantEntity>();

    public DbSet<GroupOrderEntity> GroupOrders => Set<GroupOrderEntity>();

    public DbSet<GroupChatMessageEntity> GroupChatMessages => Set<GroupChatMessageEntity>();

    public DbSet<GroupCoOrganizerEntity> GroupCoOrganizers => Set<GroupCoOrganizerEntity>();

    public DbSet<GroupReferralInviteEntity> GroupReferralInvites => Set<GroupReferralInviteEntity>();

    public DbSet<ReferralEntity> Referrals => Set<ReferralEntity>();

    public DbSet<GroupLimitRefundEntity> GroupLimitRefunds => Set<GroupLimitRefundEntity>();

    public DbSet<GroupLimitOperationEntity> GroupLimitOperations => Set<GroupLimitOperationEntity>();

    public DbSet<GroupPaymentOperationEntity> GroupPaymentOperations => Set<GroupPaymentOperationEntity>();

    public DbSet<TestCardEntity> TestCards => Set<TestCardEntity>();

    public DbSet<WalletTopUpEntity> WalletTopUps => Set<WalletTopUpEntity>();

    public Task AttachTestCardAsync(TestCardEntity card, CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"""
            INSERT INTO test_cards (id, user_id, last_four, is_active) VALUES ({card.Id}, {card.UserId}, {card.LastFour}, TRUE)
            ON CONFLICT (user_id, last_four) DO UPDATE SET is_active = TRUE
            """, cancellationToken);

    public DbSet<OrganizerLikeEntity> OrganizerLikes => Set<OrganizerLikeEntity>();

    public Task SetOrganizerLikeAsync(Guid organizerId, Guid userId, bool liked, CancellationToken cancellationToken) {
        if (liked) {
            return Database.ExecuteSqlAsync($"""
                INSERT INTO organizer_likes (organizer_id, user_id) VALUES ({organizerId}, {userId})
                ON CONFLICT (organizer_id, user_id) DO NOTHING
                """, cancellationToken);
        }

        return OrganizerLikes.Where(like => like.OrganizerId == organizerId && like.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task LockGroupPurchaseAsync(Guid groupId, CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT id FROM group_purchases WHERE id = {groupId} FOR UPDATE", cancellationToken);

    public Task LockUsersAsync(Guid[] userIds, CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT id FROM users WHERE id = ANY({userIds}) ORDER BY id FOR NO KEY UPDATE", cancellationToken);

    public async Task AddGroupPurchaseAsync(GroupPurchaseEntity group, CancellationToken cancellationToken) {
        const string alphabet = "23456789abcdefghjkmnpqrstuvwxyz";
        const int maxAttempts = 8;

        GroupPurchases.Add(group);
        for (var attempt = 1; attempt <= maxAttempts; attempt++) {
            group.InviteCode = RandomNumberGenerator.GetString(alphabet, 8);
            try {
                await SaveChangesAsync(cancellationToken);
                return;
            } catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_group_purchases_invite_code"
            }) {
                if (attempt == maxAttempts) {
                    throw;
                }
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
        try {
            return await base.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_email"
        }) {
            throw new EmailAlreadyExistsException();
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_group_purchases_creation_key"
        }) {
            throw new OperationConflictException("GROUP_CREATION_KEY_EXISTS");
        }
    }
}
