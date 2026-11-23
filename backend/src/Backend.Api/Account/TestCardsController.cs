using Backend.Api.Auth;
using Backend.Api.Contracts.Requests;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Users;
using Backend.Application.Queries.Users;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Account;

[ApiController]
[Authorize]
[Route("api/me/cards")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TestCardsController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Получить сохранённые тестовые карты")]
    public async Task<ActionResult<TestCardResponse[]>> List(CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var cards = await mediator.Send(new GetTestCardsQuery(userId), cancellationToken);
        return Ok(cards.Adapt<TestCardResponse[]>());
    }

    [HttpPost]
    [EndpointSummary("Привязать тестовую карту по четырём цифрам")]
    [ProducesResponseType<TestCardResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TestCardResponse>> Attach(AttachTestCardRequest request, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var card = await mediator.Send(new SetTestCardCommand(userId, request.LastFour), cancellationToken);
        return CreatedAtAction(nameof(List), card.Adapt<TestCardResponse>());
    }

    [HttpDelete("{cardId}")]
    [EndpointSummary("Отвязать тестовую карту")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid cardId, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        await mediator.Send(new RemoveTestCardCommand(userId, cardId), cancellationToken);
        return NoContent();
    }
}
