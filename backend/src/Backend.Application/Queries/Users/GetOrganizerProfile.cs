using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Users;

public sealed record GetOrganizerProfileQuery(Guid UserId) : IQuery<OrganizerProfileDto?> {
    public Guid? ViewerId { get; init; }
}

public sealed class GetOrganizerProfileHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetOrganizerProfileQuery, OrganizerProfileDto?> {
    public async ValueTask<OrganizerProfileDto?> Handle(
        GetOrganizerProfileQuery query,
        CancellationToken cancellationToken
    ) => await ReadAsync(dbContext, clock.GetUtcNow(), query, cancellationToken);

    internal static async Task<OrganizerProfileDto?> ReadAsync(
        IAppDbContext dbContext,
        DateTimeOffset now,
        GetOrganizerProfileQuery query,
        CancellationToken cancellationToken
    ) {
        var organizedGroups = dbContext.GroupPurchases.OrganizedBy(query.UserId);

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == query.UserId)
            .Select(user => new OrganizerProfileDto(
                user.Id,
                user.Nickname,
                organizedGroups.Count(group => group.Status == EGroupPurchaseStatus.Open && group.Deadline > now),
                organizedGroups.Count(group => group.Status == EGroupPurchaseStatus.Successful)) {
                LikesCount = user.OrganizerLikes.Count,
                LikedByMe = user.OrganizerLikes.Any(like => like.UserId == query.ViewerId)
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
