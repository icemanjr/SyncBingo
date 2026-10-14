using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SyncBingo.Api;
using SyncBingo.Api.Endpoints;
using SyncBingo.Api.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSyncBingo(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Liveness: the process is up. Readiness: Redis answers too.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

var api = app.MapGroup("/api");
api.MapPublicEndpoints();

var game = api.MapGroup("/sessions/{code}").RequireAuthorization();
game.MapQueryEndpoints();
game.MapPlayerEndpoints();
game.MapHostEndpoints();

app.MapHub<GameHub>("/hubs/game", hub => hub.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets);

app.Run();

/// <summary>Visible to the integration tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
