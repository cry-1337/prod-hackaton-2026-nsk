using Backend.Domain.Catalog;
using Backend.Domain.Groups;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Backend.Application.Common.Interfaces;

public interface IAppDbContext {
    public DatabaseFacade Database { get; }

    public DbSet<UserEntity> Users { get; }

    public DbSet<ProductEntity> Products { get; }

    public DbSet<GroupPurchaseEntity> GroupPurchases { get; }

    public DbSet<GroupPurchaseParticipantEntity> GroupPurchaseParticipants { get; }

    public DbSet<GroupOrderEntity> GroupOrders { get; }

    public DbSet<GroupChatMessageEntity> GroupChatMessages { get; }

    public DbSet<ReferralEntity> Referrals { get; }

    public DbSet<GroupLimitOperationEntity> GroupLimitOperations { get; }

    public DbSet<GroupPaymentOperationEntity> GroupPaymentOperations { get; }

    public DbSet<TestCardEntity> TestCards { get; }

    public DbSet<WalletTopUpEntity> WalletTopUps { get; }

    public Task AttachTestCardAsync(TestCardEntity card, CancellationToken cancellationToken);

    public Task LockGroupPurchaseAsync(Guid groupId, CancellationToken cancellationToken);

    public Task LockUsersAsync(Guid[] userIds, CancellationToken cancellationToken);

    public Task SetOrganizerLikeAsync(Guid organizerId, Guid userId, bool liked, CancellationToken cancellationToken);

    public Task AddGroupPurchaseAsync(GroupPurchaseEntity group, CancellationToken cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
