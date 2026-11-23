using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Account;

public sealed record GetBalanceQuery(Guid UserId) : IQuery<BalanceDto>;

public sealed class GetBalanceHandler(IAppDbContext dbContext) : IQueryHandler<GetBalanceQuery, BalanceDto> {
    public async ValueTask<BalanceDto> Handle(GetBalanceQuery query, CancellationToken cancellationToken) =>
        (await dbContext.GetWalletBalances([query.UserId]).SingleOrDefaultAsync(cancellationToken))?.Balance
            ?? throw new InvalidCredentialsException();
}
