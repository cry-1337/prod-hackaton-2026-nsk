using System.ComponentModel.DataAnnotations;
using Backend.Api.Auth;
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
[Route("api/me/notifications")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrderNotificationsController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Получить свои уведомления о покупках и возвратах")]
    [EndpointDescription("Покупка появляется после успешного завершения сбора, возврат — после фактического возврата средств. " +
        "Сначала возвращаются новые уведомления. Для непрочитанных передайте unreadOnly=true.")]
    [ProducesResponseType<OrderNotificationResponse[]>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    public async Task<ActionResult<OrderNotificationResponse[]>> List(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var notifications = await mediator.Send(new GetOrderNotificationsQuery(userId, offset, limit, unreadOnly), cancellationToken);
        return Ok(notifications.Adapt<OrderNotificationResponse[]>());
    }

    [HttpPut("{notificationId:guid}/read")]
    [EndpointSummary("Отметить своё уведомление прочитанным")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: NOTIFICATION_NOT_FOUND.")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        await mediator.Send(new MarkOrderNotificationReadCommand(userId, notificationId), cancellationToken);
        return NoContent();
    }
}
