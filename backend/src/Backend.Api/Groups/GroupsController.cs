using System.ComponentModel.DataAnnotations;
using Backend.Api.Auth;
using Backend.Api.Contracts.Requests;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Groups;
using Backend.Application.Queries.Groups;
using Backend.Domain.Groups;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Groups;

[ApiController]
[Route("api/groups")]
public sealed class GroupsController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [AllowAnonymous]
    [EndpointSummary("Лента всех сборов")]
    [EndpointDescription("По умолчанию выше сборы с большим числом лайков у организатора, при равенстве — с ближайшим дедлайном. " +
        "Для порядка от новых к старым передайте sortByOrganizerLikes=false. Без status возвращаются сборы всех статусов. Доступны open, successful и cancelled. " +
        "Начните с offset=0 и limit=20, затем увеличивайте offset на limit. Если в ответе меньше limit карточек, это последняя страница. " +
        "organizerId отбирает сборы организатора. Поиск доступен по товару, нику и коду. " +
        "maxPriceKopecks отбирает по минимальной доступной ступени или итоговой цене. endsWithinHours задаёт срок для открытых сборов.")]
    [ProducesResponseType<GroupPurchaseResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — проверьте status, offset и limit.")]
    public async Task<ActionResult<GroupPurchaseResponse[]>> List(
        [FromQuery] EGroupPurchaseStatus? status = null,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 20,
        [FromQuery] bool sortByOrganizerLikes = true,
        [FromQuery, MaxLength(256)] string? search = null,
        [FromQuery, Range(1, 168)] int? endsWithinHours = null,
        [FromQuery, Range(1, long.MaxValue)] long? maxPriceKopecks = null,
        [FromQuery] Guid? organizerId = null,
        CancellationToken cancellationToken = default
    ) {
        var groups = await mediator.Send(new GetGroupsQuery(status, offset, limit) {
            OrganizerId = organizerId,
            SortByOrganizerLikes = sortByOrganizerLikes,
            Search = search,
            EndsWithinHours = endsWithinHours,
            MaxPriceKopecks = maxPriceKopecks
        }, cancellationToken);
        return Ok(groups.Select(group => group.ToResponse(Url)).ToArray());
    }

    [HttpPost]
    [Authorize]
    [EndpointSummary("Создать сбор по товару")]
    [EndpointDescription("Передайте productId, будущий deadline и organizerLimitKopecks — цену выбранной ступени в копейках. Для доставки обязательны населённый пункт, улица и дом. " +
        "Это личный максимум организатора. Сбор сохраняет все цены товара. При успехе организатор получает подарок из товара. " +
        "Предоплата организатора списывается при создании. Повтор использует тот же Idempotency-Key.")]
    [ProducesResponseType<GroupPurchaseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "VALIDATION_FAILED — подробности по полям в errors. PRICE_TIER_UNAVAILABLE означает, что у товара нет выбранной ступени.")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json", Description = "title: UNAUTHORIZED — требуется Bearer JWT; INVALID_CREDENTIALS — профиль организатора не найден.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: PRODUCT_NOT_FOUND — товар не найден.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: INSUFFICIENT_BALANCE — не хватает средств для предоплаты, IDEMPOTENCY_KEY_REUSED — ключ уже использован с другими параметрами, PRODUCT_HAS_NO_GROUP_PRICES — у товара нет ступеней цены.")]
    public async Task<ActionResult<GroupPurchaseResponse>> Create(
        [FromBody] CreateGroupPurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var command = request.Adapt<CreateGroupPurchaseCommand>() with {
            OrganizerId = userId,
            CreationKey = idempotencyKey?.Trim(),
            Address = request.Address.ToAddress()
        };
        var group = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { groupId = group.Id }, group.ToResponse(Url));
    }

    [HttpPost("{groupId}/join")]
    [Authorize]
    [EndpointSummary("Вступить в сбор с тестовой оплатой")]
    [EndpointDescription("Передайте адрес доставки, код сбора и Bearer-токен участника. " +
        "Сервер списывает цену с тестового кошелька. Повторный запрос сохраняет одно участие.")]
    [ProducesResponseType<GroupPurchaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json", Description = "UNAUTHORIZED — требуется Bearer-токен. INVALID_CREDENTIALS — профиль пользователя отсутствует.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: GROUP_NOT_FOUND.")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json", Description = "title: INSUFFICIENT_BALANCE, GROUP_CLOSED, ALREADY_JOINED.")]
    public async Task<ActionResult<GroupPurchaseResponse>> Join(
        string groupId,
        [FromBody] JoinGroupPurchaseRequest request,
        [FromQuery] decimal quantity = 1m,
        CancellationToken cancellationToken = default
    ) {
        if (!User.TryGetUserId(out var userId)) {
            return Challenge();
        }

        var group = await mediator.Send(new JoinGroupPurchaseCommand(groupId, userId) { Quantity = quantity, Address = request.Address.ToAddress() }, cancellationToken);
        return Ok(group.ToResponse(Url));
    }

    [HttpGet("{groupId}/participants")]
    [AllowAnonymous]
    [EndpointSummary("Публичные профили и цены участия за единицу товара")]
    [ProducesResponseType<GroupParticipantResponse[]>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public async Task<ActionResult<GroupParticipantResponse[]>> Participants(
        string groupId,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 20,
        CancellationToken cancellationToken = default
    ) {
        var members = await mediator.Send(new GetGroupParticipantsQuery(groupId, offset, limit), cancellationToken);
        return Ok(members.Adapt<GroupParticipantResponse[]>());
    }

    [HttpGet("{groupId}", Name = InviteLinks.RouteName)]
    [AllowAnonymous]
    [EndpointSummary("Открыть сбор по публичному коду")]
    [EndpointDescription("Передайте id из ленты, приглашения или ответа на создание. Регистр кода не важен. Закрытый сбор остаётся доступным. Просмотр не добавляет участника.")]
    [ProducesResponseType<GroupPurchaseResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: GROUP_NOT_FOUND — сбор с таким кодом не найден.")]
    public async Task<ActionResult<GroupPurchaseResponse>> Get(string groupId, CancellationToken cancellationToken) {
        var group = await mediator.Send(new GetGroupPurchaseQuery(groupId), cancellationToken);
        return group is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "GROUP_NOT_FOUND")
            : Ok(group.ToResponse(Url));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("/invite/{inviteCode}")]
    [HttpGet("invite/{inviteCode}")]
    [AllowAnonymous]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupPurchaseResponse>> GetByInvite(string inviteCode, CancellationToken cancellationToken) {
        var group = await mediator.Send(new GetGroupPurchaseQuery(inviteCode, InviteCodeOnly: true), cancellationToken);
        return group is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "INVITATION_NOT_FOUND")
            : Ok(group.ToResponse(Url));
    }
}
