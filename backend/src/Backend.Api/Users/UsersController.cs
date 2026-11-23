using Backend.Api.Auth;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Users;
using Backend.Application.Queries.Users;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Users;

[ApiController]
[Route("api/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(IMediator mediator) : ControllerBase {
    [HttpGet("{userId}/organizer-profile")]
    [AllowAnonymous]
    [EndpointSummary("Получить публичный профиль организатора")]
    [EndpointDescription("Передайте organizerId из карточки сбора. Профиль содержит число лайков. С Bearer-токеном likedByMe показывает, поставили ли вы лайк.")]
    [ProducesResponseType<OrganizerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — userId должен быть UUID.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: USER_NOT_FOUND — пользователь не найден.")]
    public async Task<ActionResult<OrganizerProfileResponse>> GetOrganizerProfile(Guid userId, CancellationToken cancellationToken) {
        var profile = await mediator.Send(new GetOrganizerProfileQuery(userId) {
            ViewerId = User.TryGetUserId(out var viewerId) ? viewerId : null
        }, cancellationToken);
        return profile is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "USER_NOT_FOUND")
            : Ok(profile.Adapt<OrganizerProfileResponse>());
    }

    [HttpPut("{userId}/organizer-profile/like")]
    [Authorize]
    [ProducesResponseType<OrganizerProfileResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [EndpointSummary("Поставить лайк организатору")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: CANNOT_LIKE_SELF — нельзя поставить лайк своему профилю.")]
    public Task<ActionResult<OrganizerProfileResponse>> LikeOrganizer(Guid userId, CancellationToken cancellationToken) =>
        SetLikeAsync(userId, true, cancellationToken);

    [HttpDelete("{userId}/organizer-profile/like")]
    [Authorize]
    [ProducesResponseType<OrganizerProfileResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [EndpointSummary("Снять лайк с организатора")]
    public Task<ActionResult<OrganizerProfileResponse>> UnlikeOrganizer(Guid userId, CancellationToken cancellationToken) =>
        SetLikeAsync(userId, false, cancellationToken);

    private async Task<ActionResult<OrganizerProfileResponse>> SetLikeAsync(Guid organizerId, bool liked, CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var profile = await mediator.Send(new SetOrganizerLikeCommand(organizerId, userId, liked), cancellationToken);
        return Ok(profile.Adapt<OrganizerProfileResponse>());
    }
}
