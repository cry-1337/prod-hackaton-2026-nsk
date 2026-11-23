using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Backend.Api.Auth;
using Backend.Api.Contracts.Responses;
using Backend.Application.Commands.Groups;
using Backend.Application.Common.Exceptions;
using Backend.Application.Dto;
using Backend.Application.Queries.Groups;
using FluentValidation;
using Mapster;
using Mediator;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Backend.Api.Groups;

internal sealed class GroupChatWebSocketHandler(
    IServiceScopeFactory scopeFactory,
    GroupChatWebSocketHub hub,
    [FromKeyedServices(AuthenticationExtensions.ChatWritePolicy)] PartitionedRateLimiter<Guid> rateLimiter,
    JwtTokens jwtTokens,
    TimeProvider clock,
    IHostApplicationLifetime lifetime,
    ILogger<GroupChatWebSocketHandler> logger
) {
    private const int m_MaxFrameBytes = 8 * 1024;
    private const int m_PageSize = 50;
    private static readonly JsonSerializerOptions m_JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(HttpContext context, string identifier, long? afterId, CancellationToken cancellationToken) {
        if (!context.WebSockets.IsWebSocketRequest || afterId is < 0) {
            await Results.Problem(statusCode: 400, title: "CHAT_UPGRADE_INVALID").ExecuteAsync(context);
            return;
        }

        var groupId = await SendAsync(new ResolveGroupChatQuery(identifier), cancellationToken);
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.ApplicationStopping);
        var connectionToken = connection.Token;
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var notifications = hub.Subscribe(groupId);
        try {
            await RunAsync(socket, context.User, groupId, identifier, afterId, notifications.Reader, connectionToken);
        } catch (WebSocketException exception) {
            logger.LogDebug(exception, "Group chat WebSocket disconnected");
        } catch (OperationCanceledException) {
        } finally {
            connection.Cancel();
            hub.Unsubscribe(groupId, notifications.Writer);
            if (socket.State == WebSocketState.CloseReceived) {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, closeTimeout.Token);
                } catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) {
                    logger.LogDebug(exception, "Group chat WebSocket close failed");
                }
            }
        }
    }

    private async Task RunAsync(
        WebSocket socket,
        ClaimsPrincipal user,
        Guid groupId,
        string identifier,
        long? afterId,
        ChannelReader<bool> notifications,
        CancellationToken cancellationToken
    ) {
        var identity = user;
        var initial = await SendAsync(new GetGroupChatMessagesQuery(identifier, null, afterId, m_PageSize), cancellationToken);
        await SendFrameAsync(socket, new { type = "ready", data = initial.Adapt<GroupChatMessageResponse[]>() }, cancellationToken);
        var cursor = initial.LastOrDefault()?.Id ?? afterId ?? 0;
        var incoming = ReadFrameAsync(socket, cancellationToken);
        var changed = notifications.WaitToReadAsync(cancellationToken).AsTask();
        var refresh = afterId.HasValue && initial.Count == m_PageSize;

        while (!cancellationToken.IsCancellationRequested) {
            if (!refresh && !changed.IsCompleted && !incoming.IsCompleted) {
                await Task.WhenAny(changed, incoming);
            }

            if (incoming.IsCompleted) {
                var (frame, closeStatus) = await incoming;
                if (closeStatus is { } status) {
                    using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await socket.CloseOutputAsync(status, null, closeTimeout.Token);
                    return;
                }

                if (frame is null) {
                    return;
                }

                try {
                    var request = JsonSerializer.Deserialize<ChatRequest>(frame, m_JsonOptions);
                    identity = await ProcessAsync(socket, identity, groupId, identifier, request, cancellationToken);
                } catch (JsonException) {
                    await SendErrorAsync(socket, 400, "CHAT_MESSAGE_INVALID", cancellationToken);
                } catch (ValidationException) {
                    await SendErrorAsync(socket, 400, "VALIDATION_FAILED", cancellationToken);
                } catch (AccessDeniedException exception) {
                    await SendErrorAsync(socket, 403, exception.Message, cancellationToken);
                } catch (OperationConflictException exception) {
                    await SendErrorAsync(socket, 409, exception.Message, cancellationToken);
                } catch (EntityNotFoundException exception) {
                    await SendErrorAsync(socket, 404, exception.Message, cancellationToken);
                } catch (InvalidCredentialsException exception) {
                    await SendErrorAsync(socket, 401, exception.Message, cancellationToken);
                }

                incoming = ReadFrameAsync(socket, cancellationToken);
            }

            if (changed.IsCompleted) {
                await changed;
                notifications.TryRead(out _);
                changed = notifications.WaitToReadAsync(cancellationToken).AsTask();
                refresh = true;
            }

            if (refresh) {
                var messages = await SendAsync(new GetGroupChatMessagesQuery(identifier, null, cursor, m_PageSize), cancellationToken);
                foreach (var message in messages) {
                    await SendFrameAsync(socket, new { type = "message", data = message.Adapt<GroupChatMessageResponse>() }, cancellationToken);
                    cursor = message.Id;
                }

                refresh = messages.Count >= m_PageSize;
            }
        }
    }

    private async Task<ClaimsPrincipal> ProcessAsync(
        WebSocket socket,
        ClaimsPrincipal user,
        Guid groupId,
        string identifier,
        ChatRequest? request,
        CancellationToken cancellationToken
    ) {
        switch (request?.Type) {
            case "auth":
                var validation = await new JsonWebTokenHandler().ValidateTokenAsync(request.AccessToken ?? string.Empty,
                    jwtTokens.ValidationParameters());
                if (!validation.IsValid) {
                    await SendErrorAsync(socket, 401, "UNAUTHORIZED", cancellationToken);
                    return new ClaimsPrincipal();
                }

                await SendFrameAsync(socket, new { type = "authenticated" }, cancellationToken);
                return new ClaimsPrincipal(validation.ClaimsIdentity);

            case "history":
                var history = await SendAsync(new GetGroupChatMessagesQuery(identifier, request.BeforeId, request.AfterId,
                    request.Limit ?? m_PageSize), cancellationToken);
                await SendFrameAsync(socket, new { type = "history", data = history.Adapt<GroupChatMessageResponse[]>() }, cancellationToken);
                break;

            case "message":
                if (!user.TryGetUserId(out var userId) ||
                    !long.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Exp), out var expiration) ||
                    expiration <= clock.GetUtcNow().ToUnixTimeSeconds()) {
                    await SendErrorAsync(socket, 401, "UNAUTHORIZED", cancellationToken);
                    break;
                }

                using (var lease = rateLimiter.AttemptAcquire(userId)) {
                    if (!lease.IsAcquired) {
                        await SendErrorAsync(socket, 429, "RATE_LIMIT_EXCEEDED", cancellationToken);
                        break;
                    }

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var message = await sender.Send(new PostGroupChatMessageCommand(identifier, userId,
                        request.Text?.Trim() ?? string.Empty, request.IdempotencyKey?.Trim()), cancellationToken);
                    hub.Notify(groupId);
                    await SendFrameAsync(socket, new { type = "sent", data = message.Adapt<GroupChatMessageResponse>() }, cancellationToken);
                }
                break;

            case "ping":
                await SendFrameAsync(socket, new { type = "pong" }, cancellationToken);
                break;

            default:
                await SendErrorAsync(socket, 400, "CHAT_COMMAND_INVALID", cancellationToken);
                break;
        }

        return user;
    }

    private async Task<T> SendAsync<T>(IQuery<T> request, CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, cancellationToken);
    }

    private static async Task<(byte[]? Payload, WebSocketCloseStatus? CloseStatus)> ReadFrameAsync(
        WebSocket socket,
        CancellationToken cancellationToken
    ) {
        var buffer = new byte[m_MaxFrameBytes];
        var length = 0;
        while (true) {
            var result = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) {
                return (null, null);
            }

            if (result.MessageType != WebSocketMessageType.Text || (!result.EndOfMessage && length + result.Count == buffer.Length)) {
                return (null, result.MessageType == WebSocketMessageType.Text
                    ? WebSocketCloseStatus.MessageTooBig : WebSocketCloseStatus.InvalidMessageType);
            }

            length += result.Count;
            if (result.EndOfMessage) {
                return (buffer[..length], null);
            }
        }
    }

    private static Task SendErrorAsync(WebSocket socket, int status, string title, CancellationToken cancellationToken) =>
        SendFrameAsync(socket, new { type = "error", status, title }, cancellationToken);

    private static async Task SendFrameAsync<T>(WebSocket socket, T payload, CancellationToken cancellationToken) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(payload, m_JsonOptions).AsMemory(), WebSocketMessageType.Text,
            true, timeout.Token);
    }

    private sealed record ChatRequest(
        string? Type,
        string? AccessToken,
        string? Text,
        string? IdempotencyKey,
        long? BeforeId,
        long? AfterId,
        int? Limit
    );
}
