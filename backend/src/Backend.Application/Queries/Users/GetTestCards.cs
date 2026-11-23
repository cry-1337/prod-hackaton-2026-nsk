using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Users;

public sealed record GetTestCardsQuery(Guid UserId) : IQuery<TestCardDto[]>;

public sealed class GetTestCardsHandler(IAppDbContext dbContext) : IQueryHandler<GetTestCardsQuery, TestCardDto[]> {
    public async ValueTask<TestCardDto[]> Handle(GetTestCardsQuery query, CancellationToken cancellationToken) =>
        await dbContext.TestCards.Where(card => card.UserId == query.UserId && card.IsActive).OrderBy(card => card.Id)
            .Select(card => new TestCardDto(card.Id, card.LastFour)).ToArrayAsync(cancellationToken);
}
