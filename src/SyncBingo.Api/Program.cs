using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SyncBingo.Api;
using SyncBingo.Api.Endpoints;
using SyncBingo.Api.Http;
using SyncBingo.Api.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSyncBingo(builder.Configuration);
builder.Services.AddProxyHeaders();

var app = builder.Build();

// First, so everything after it (the rate limits above all) sees the client's own address.
if (ProxyHeaders.Enabled(app.Configuration))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

// The API description (OpenAPI 3.1), from which the web app's typed client is generated.
app.MapOpenApi();

// Liveness: the process is up. Readiness: Redis answers too.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Every API endpoint may refuse with a problem (docs/05 §5); the typed web client relies on this.
var api = app.MapGroup("/api")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .ProducesProblem(StatusCodes.Status429TooManyRequests);
api.MapPublicEndpoints();
api.MapTvEndpoints();

var game = api.MapGroup("/sessions/{code}")
    .RequireAuthorization()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden);
game.MapQueryEndpoints();
game.MapPlayerEndpoints();
game.MapHostEndpoints();
game.MapDisplayEndpoints();

app.MapHub<GameHub>("/hubs/game", hub => hub.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets);
app.MapHub<PairingHub>("/hubs/pairing", hub => hub.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets);

app.Run();

/// <summary>Visible to the integration tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
