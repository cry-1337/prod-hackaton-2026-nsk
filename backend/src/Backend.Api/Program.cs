using Backend.Api;
using Backend.Api.Groups;
using Backend.Infrastructure;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackend(builder.Configuration);

var app = builder.Build();

app.UseRouting();
var webSockets = new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    KeepAliveTimeout = TimeSpan.FromSeconds(15)
};
if (InviteLinks.GetFrontendOrigin(app.Configuration["Invitations:UrlTemplate"]) is { } frontendOrigin) {
    webSockets.AllowedOrigins.Add(frontendOrigin);
}
app.UseWebSockets(webSockets);
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api") && !context.WebSockets.IsWebSocketRequest, api =>
    api.UseHttpMetrics(options => options.ReduceStatusCodeCardinality()));
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages(async context => {
    var status = context.HttpContext.Response.StatusCode;
    var title = status switch {
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status429TooManyRequests => "RATE_LIMIT_EXCEEDED",
        _ => null
    };
    if (title is not null) {
        await Results.Problem(statusCode: status, title: title).ExecuteAsync(context.HttpContext);
    }
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.Map("/api/groups/{groupId}/chat/ws", (
    HttpContext context, string groupId, long? afterId, GroupChatWebSocketHandler handler, CancellationToken cancellationToken) =>
    handler.HandleAsync(context, groupId, afterId, cancellationToken)).AllowAnonymous().ExcludeFromDescription();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapMetrics("/metrics").AllowAnonymous().ExcludeFromDescription();

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue("Documentation:Enabled", app.Environment.IsDevelopment())) {
    const string schemaRoute = "/api/openapi/{documentName}.json";
    app.MapOpenApi(schemaRoute);
    app.MapScalarApiReference("/api/scalar", options => options.WithOpenApiRoutePattern(schemaRoute));
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")) {
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
