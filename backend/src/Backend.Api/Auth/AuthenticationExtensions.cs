using System.Text.Json;
using System.Threading.RateLimiting;
using Backend.Api.Contracts.Responses;
using Backend.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Backend.Api.Auth;

internal static class AuthenticationExtensions {
    public const string CredentialsPolicy = "credentials";
    public const string ChatWritePolicy = "chat-write";

    public static void AddBackendAuthentication(this IServiceCollection services) {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtTokens>();
        services.AddSingleton<IAccessTokenIssuer>(provider => provider.GetRequiredService<JwtTokens>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtTokens>((options, tokens) => {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = tokens.ValidationParameters();
            })
            .ValidateOnStart();

        services.AddAuthorization();

        services.AddOpenApi(options => {
            options.AddSchemaTransformer((schema, context, _) => {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

                if (type.IsEnum && schema.Enum is { Count: > 0 } values &&
                    values.All(value => value?.GetValueKind() is null or JsonValueKind.String)) {
                    schema.Type = JsonSchemaType.String;
                    schema.Enum = [.. values.Where(value => value is not null)];
                }

                if (type == typeof(GroupPurchaseResponse) && schema.Properties?["inviteCode"] is OpenApiSchema inviteCode) {
                    inviteCode.Type = JsonSchemaType.String;
                    inviteCode.ReadOnly = true;
                    schema.Required ??= new HashSet<string>();
                    schema.Required.UnionWith(["inviteCode", "inviteUrl"]);
                }

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, context, cancellationToken) => {
                document.Info.Title = "Вместе — API групповых закупок";
                document.Info.Description = "Откройте GET /api/groups для ленты или GET /api/catalog/products для выбора товара. " +
                    "Для создания сбора и просмотра личного кабинета получите accessToken через /api/auth/login или /api/auth/register и вставьте его в Bearer. " +
                    "Цены передаются целыми копейками: priceKopecks = 99000 означает 990 ₽. " +
                    "В ошибках используйте status и точное значение title из описания ответа. При VALIDATION_FAILED детали по полям находятся в errors.";
                document.Servers = [new OpenApiServer { Url = "/" }];
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) => {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;

                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any()) {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement {
                        [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                    });
                }

                return Task.CompletedTask;
            });
        });

        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(CredentialsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1)
                }));

        });

        services.AddKeyedSingleton(ChatWritePolicy, (_, _) => PartitionedRateLimiter.Create<Guid, Guid>(userId =>
            RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            })));
    }
}
