using JeepneyFareCalculator.Api;
using JeepneyFareCalculator.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ProblemDetails is the single error response format: unhandled exceptions
// and empty error responses (e.g. 404) are all written as ProblemDetails.
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

app.Run();
