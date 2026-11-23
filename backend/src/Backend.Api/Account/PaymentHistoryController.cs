using Backend.Api.Auth;
using Backend.Api.Contracts.Responses;
using Backend.Application.Queries.Account;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Account;

[ApiController]
[Authorize]
[Route("api/me/payments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentHistoryController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Получить историю тестовых списаний, возвратов и кэшбэка")]
    public async Task<ActionResult<PaymentHistoryResponse>> Get(CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var history = await mediator.Send(new GetPaymentHistoryQuery(userId), cancellationToken);
        return Ok(history.Adapt<PaymentHistoryResponse>());
    }
}
