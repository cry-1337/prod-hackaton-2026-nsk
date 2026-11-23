using Backend.Api.Auth;
using Backend.Api.Contracts.Responses;
using Backend.Api.Groups;
using Backend.Application.Queries.Account;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Account;

[ApiController]
[Route("api/me/groups")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Мои сборы и роль в каждом из них")]
    [EndpointDescription("Вкладка «Участвую» включает все мои сборы. Для вкладки «Организую» выберите myRole = organizer. Список включает завершённые сборы.")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json", Description = "title: UNAUTHORIZED — требуется действующий Bearer JWT.")]
    [ProducesResponseType<GroupPurchaseResponse[]>(200)]
    public Task<ActionResult<GroupPurchaseResponse[]>> Get(CancellationToken cancellationToken) =>
        GetGroupsAsync(null, cancellationToken);

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("/api/account/organizing")]
    public Task<ActionResult<GroupPurchaseResponse[]>> Organizing(CancellationToken cancellationToken) =>
        GetGroupsAsync(EGroupRole.Organizer, cancellationToken);

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("/api/account/participating")]
    public Task<ActionResult<GroupPurchaseResponse[]>> Participating(CancellationToken cancellationToken) =>
        GetGroupsAsync(EGroupRole.Participant, cancellationToken);

    private async Task<ActionResult<GroupPurchaseResponse[]>> GetGroupsAsync(EGroupRole? role, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var groups = await mediator.Send(new GetMyGroupsQuery(userId, role), cancellationToken);
        var responses = groups.Select(group => {
            var response = group.ToResponse(Url);
            if (role.HasValue) {
                return response;
            }

            return response with {
                MyRole = group.OrganizerId == userId ? EGroupRole.Organizer : EGroupRole.Participant
            };
        });
        return Ok(responses.ToArray());
    }
}
