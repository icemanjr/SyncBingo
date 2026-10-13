using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SyncBingo.Api;
using SyncBingo.Api.Endpoints;

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
game.MapPlayerEndpoints();
game.MapHostEndpoints();

app.Run();

/// <summary>Visible to the integration tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
