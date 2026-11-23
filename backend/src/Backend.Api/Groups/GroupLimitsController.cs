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
[Route("api/groups/{groupId}/limit")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GroupLimitsController(IMediator mediator) : ControllerBase {
    [HttpGet("quote")]
    [ProducesResponseType<GroupLimitQuoteResponse>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [EndpointSummary("Рассчитать заявку «Куплю до…»")]
    [EndpointDescription("limitKopecks принимает одну из ценовых ступеней сбора за единицу товара, quantity — количество в единицах товара. Расчёт показывает сумму списания за весь заказ.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: GROUP_CLOSED или ALREADY_JOINED.")]
    public async Task<ActionResult<GroupLimitQuoteResponse>> Quote(
        string groupId,
        [FromQuery] long limitKopecks,
        [FromQuery] decimal quantity = 1m,
        CancellationToken cancellationToken = default
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var quote = await mediator.Send(new GetGroupLimitQuoteQuery(groupId, userId, limitKopecks, quantity), cancellationToken);
        return Ok(quote.Adapt<GroupLimitQuoteResponse>());
    }

    [HttpGet]
    [ProducesResponseType<GroupLimitResponse>(200)]
    [EndpointSummary("Посмотреть свою заявку и результат покупки")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: LIMIT_NOT_FOUND или GROUP_NOT_FOUND.")]
    public async Task<ActionResult<GroupLimitResponse>> Get(string groupId, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var result = await mediator.Send(new GetGroupLimitQuery(groupId, userId), cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "LIMIT_NOT_FOUND")
            : Ok(result.Adapt<GroupLimitResponse>());
    }

    [HttpPut]
    [ProducesResponseType<GroupLimitResponse>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [EndpointSummary("Подтвердить или изменить заявку «Куплю до…»")]
    [EndpointDescription("Для доставки обязательны населённый пункт, улица и дом. Предоплата списывается при подтверждении. Переплата возвращается при завершении по более низкой цене. Выберите цену из ступеней сбора. Порог определяется количеством товара в прямых покупках и подходящих предоплаченных заявках.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: GROUP_NOT_FOUND, TEST_CARD_NOT_FOUND.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: INSUFFICIENT_BALANCE, GROUP_CLOSED, ALREADY_JOINED, IDEMPOTENCY_KEY_REUSED.")]
    public Task<ActionResult<GroupLimitResponse>> Set(
        string groupId,
        SetGroupLimitRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken cancellationToken
    ) => ChangeAsync(groupId, request, key, cancellationToken);

    [HttpDelete]
    [ProducesResponseType<GroupLimitResponse>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    [EndpointSummary("Отозвать ожидающую заявку")]
    public Task<ActionResult<GroupLimitResponse>> Revoke(
        string groupId,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken cancellationToken
    ) => ChangeAsync(groupId, null, key, cancellationToken);

    private async Task<ActionResult<GroupLimitResponse>> ChangeAsync(
        string groupId,
        SetGroupLimitRequest? request,
        string? key,
        CancellationToken cancellationToken = default
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var result = await mediator.Send(new ChangeGroupLimitCommand(groupId, userId, request?.LimitKopecks, key?.Trim()) {
            Quantity = request?.Quantity ?? 1m,
            CardId = request?.CardId,
            Address = request?.Address.ToAddress(),
        }, cancellationToken);
        return Ok(result.Adapt<GroupLimitResponse>());
    }
}
