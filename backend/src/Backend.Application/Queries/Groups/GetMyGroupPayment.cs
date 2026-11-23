using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetMyGroupPaymentQuery(string Identifier, Guid UserId) : IQuery<GroupPaymentDto?>;

public sealed class GetMyGroupPaymentHandler(IAppDbContext dbContext)
    : IQueryHandler<GetMyGroupPaymentQuery, GroupPaymentDto?> {
    public async ValueTask<GroupPaymentDto?> Handle(GetMyGroupPaymentQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases.WithDetails().ByIdentifier(query.Identifier)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
        return group.GetPayment(query.UserId);
    }
}
