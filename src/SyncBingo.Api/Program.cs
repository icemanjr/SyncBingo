var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness probe for Compose and the ALB; /readyz (with a Redis check) follows with the Redis store.
app.MapHealthChecks("/healthz");

app.Run();
