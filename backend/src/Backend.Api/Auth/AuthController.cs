using Backend.Api.Contracts.Requests;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Auth;
using Backend.Application.Queries.Auth;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Api.Auth;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IMediator mediator) : ControllerBase {
    [HttpPost("register")]
    [EndpointSummary("Создать профиль и получить токен")]
    [EndpointDescription("Передайте email, nickname (до 64 символов) и password (12–128 символов). Ответ содержит профиль и accessToken, действующий один час.")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationExtensions.CredentialsPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — проверьте поля в errors.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: EMAIL_ALREADY_EXISTS — почта занята.")]
    [ProducesResponseType<ProblemDetails>(429, "application/problem+json", Description = "title: RATE_LIMIT_EXCEEDED — исчерпан общий лимит регистрации и входа (20 запросов в минуту с IP).")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken
    ) {
        var command = request.Adapt<RegisterCommand>();
        var result = await mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(Me), result.Adapt<AuthResponse>());
    }

    [HttpPost("login")]
    [EndpointSummary("Войти по почте и паролю")]
    [EndpointDescription("Скопируйте accessToken из ответа в поле Bearer в Scalar. В запросах клиента используйте заголовок Authorization: Bearer <accessToken>.")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationExtensions.CredentialsPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — проверьте поля в errors.")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json", Description = "title: INVALID_CREDENTIALS — неверная почта или пароль.")]
    [ProducesResponseType<ProblemDetails>(429, "application/problem+json", Description = "title: RATE_LIMIT_EXCEEDED — исчерпан общий лимит регистрации и входа (20 запросов в минуту с IP).")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    ) {
        var command = request.Adapt<LoginCommand>();
        var result = await mediator.Send(command, cancellationToken);

        return Ok(result.Adapt<AuthResponse>());
    }

    [HttpGet("me")]
    [EndpointSummary("Получить свой профиль")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json", Description = "title: UNAUTHORIZED — требуется действующий токен пользователя.")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var user = await mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);

        if (user is null) {
            return Challenge();
        }

        return Ok(user.Adapt<UserResponse>());
    }
}
