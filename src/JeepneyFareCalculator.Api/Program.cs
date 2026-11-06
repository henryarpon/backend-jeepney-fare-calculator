using JeepneyFareCalculator.Api;
using System.Text.Json.Serialization;
using JeepneyFareCalculator.Api.Authentication;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// A one-off tool instead of the API: prints a hash for Administrator:PasswordHash.
if (args is [HashPasswordCommand.Name])
{
    return HashPasswordCommand.Run();
}

var builder = WebApplication.CreateBuilder(args);

// Enums such as VehicleType are read and written by name ("TraditionalJeepney"), never as numbers.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// ProblemDetails is the single error response format: unhandled exceptions
// and empty error responses (e.g. 404) are all written as ProblemDetails.
builder.Services.AddProblemDetails();

// A rejected fare matrix file is answered with 400 and its errors, not 500.
builder.Services.AddExceptionHandler<InvalidFareMatrixFileHandler>();

builder.Services.AddInfrastructure();
builder.Services.AddAdminAuthentication();

builder.Services.AddSingleton(TimeProvider.System);

// Application use cases.
builder.Services.AddScoped<PreviewFareMatrix>();
builder.Services.AddScoped<UploadFareMatrix>();
builder.Services.AddScoped<ListFareMatrixVersions>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

app.Run();
return 0;
