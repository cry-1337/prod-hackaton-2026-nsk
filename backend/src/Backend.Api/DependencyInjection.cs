using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Api.Auth;
using Backend.Api.Catalog;
using Backend.Api.Errors;
using Backend.Api.Groups;
using Backend.Application.Behaviors;
using Backend.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

namespace Backend.Api;

internal static class DependencyInjection {
    public static IServiceCollection AddBackend(this IServiceCollection services, IConfiguration configuration) {
        Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions {
            SuppressDebugMetrics = true,
            SuppressEventCounters = true,
            SuppressMeters = true
        });

        services.AddSingleton(new ProductImages(configuration["Storage:PublicBucketUrl"]));

        services.AddSerilog((provider, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        var frontendOrigin = InviteLinks.GetFrontendOrigin(configuration["Invitations:UrlTemplate"]);
        services.AddCors(options => {
            if (frontendOrigin is not null) {
                options.AddDefaultPolicy(policy => policy.WithOrigins(frontendOrigin)
                    .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
                    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
                    .WithExposedHeaders("Location"));
            }
        });

        var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(enumConverter));
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options => {
                options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                    new ValidationProblemDetails(context.ModelState) {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "VALIDATION_FAILED"
                    }) { ContentTypes = { "application/problem+json" } };
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(enumConverter));
        services.AddHealthChecks();
        services.AddSingleton<GroupChatWebSocketHub>();
        services.AddScoped<GroupChatWebSocketHandler>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddBackendAuthentication();

        services.AddValidatorsFromAssembly(typeof(ValidationBehavior<,>).Assembly);
        services.AddMediator(options => {
            options.Assemblies = [typeof(ValidationBehavior<,>)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
        });

        return services.AddInfrastructure(
            configuration.GetConnectionString("Postgres"),
            configuration.GetConnectionString("Redis"));
    }
}
