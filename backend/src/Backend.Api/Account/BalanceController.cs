using Backend.Api.Auth;
using Backend.Api.Contracts.Requests;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Account;
using Backend.Application.Queries.Account;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Account;

[ApiController]
[Authorize]
[Route("api/me/balance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BalanceController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Получить свой тестовый баланс")]
    [EndpointDescription("Суммы передаются в копейках. Баланс учитывает тестовые пополнения, списания и возвраты кошелька, кэшбэк и реферальные бонусы.")]
    [ProducesResponseType<BalanceResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<ActionResult<BalanceResponse>> Get(CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var balance = await mediator.Send(new GetBalanceQuery(userId), cancellationToken);
        return Ok(balance.Adapt<BalanceResponse>());
    }

    [HttpPost("top-ups")]
    [EndpointSummary("Пополнить свой тестовый кошелёк")]
    [EndpointDescription("Передайте сумму в копейках и Idempotency-Key. Повтор с тем же ключом и суммой возвращает прежнее пополнение.")]
    [ProducesResponseType<WalletTopUpResponse>(201)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: IDEMPOTENCY_KEY_REUSED.")]
    public async Task<ActionResult<WalletTopUpResponse>> TopUp(
        TopUpWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken cancellationToken
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var topUp = await mediator.Send(new TopUpWalletCommand(userId, request.AmountKopecks, key?.Trim()), cancellationToken);
        return CreatedAtAction(nameof(Get), topUp.Adapt<WalletTopUpResponse>());
    }
}
