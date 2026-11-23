using Backend.Api.Auth;
using Backend.Api.Contracts.Requests;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Groups;
using Backend.Application.Queries.Groups;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Groups;

[ApiController]
[Authorize]
[Route("api/groups/{groupId}/payment")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GroupPaymentsController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Посмотреть свою оплату, возврат или награду в сборе")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: GROUP_NOT_FOUND или PAYMENT_NOT_FOUND.")]
    public async Task<ActionResult<GroupPaymentResponse>> Get(string groupId, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var payment = await mediator.Send(new GetMyGroupPaymentQuery(groupId, userId), cancellationToken);
        return payment is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "PAYMENT_NOT_FOUND")
            : Ok(payment.Adapt<GroupPaymentResponse>());
    }

    [HttpGet("quote")]
    [EndpointSummary("Рассчитать прямую тестовую оплату")]
    public async Task<ActionResult<GroupPaymentQuoteResponse>> Quote(string groupId, [FromQuery] decimal quantity = 1m, CancellationToken cancellationToken = default) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var quote = await mediator.Send(new GetGroupPaymentQuoteQuery(groupId, userId, quantity), cancellationToken);
        return Ok(quote.Adapt<GroupPaymentQuoteResponse>());
    }

    [HttpPost]
    [EndpointSummary("Подтвердить тестовую оплату")]
    [EndpointDescription("При снижении общей цены разница за всё количество возвращается на кошелёк. Передайте подтверждённую цену и адрес с населённым пунктом, улицей и домом, cardId можно опустить.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: GROUP_NOT_FOUND, TEST_CARD_NOT_FOUND.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: INSUFFICIENT_BALANCE, PRICE_CONFIRMATION_REQUIRED, GROUP_CLOSED, ALREADY_JOINED, IDEMPOTENCY_KEY_REUSED.")]
    public async Task<ActionResult<GroupPurchaseResponse>> Pay(
        string groupId,
        PayGroupPurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken cancellationToken
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var group = await mediator.Send(new JoinGroupPurchaseCommand(groupId, userId) {
            ConfirmedPriceKopecks = request.ConfirmedPriceKopecks,
            Quantity = request.Quantity,
            CardId = request.CardId,
            Address = request.Address.ToAddress(),
            IdempotencyKey = key?.Trim()
        }, cancellationToken);
        return Ok(group.ToResponse(Url));
    }
}
