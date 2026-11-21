using System.Net;
using System.Net.Http.Json;
using JeepneyFareCalculator.Application.Administrators;
using JeepneyFareCalculator.Application.Distances;
using JeepneyFareCalculator.Infrastructure;
using JeepneyFareCalculator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the whole API in memory for integration tests.
/// Each test run gets its own throwaway LocalDB database, which is created
/// on start-up and dropped when the run ends. The distance estimator is the
/// only collaborator replaced: tests control it through <see cref="DistanceEstimator"/>.
/// </summary>
/// <remarks>
/// This factory replaces the whole distance estimator, cache included, so a test
/// always sees the <see cref="FakeDistanceEstimator.NextEstimate"/> it set.
/// <see cref="RoutingQuotaApiFactory"/> keeps the cache.
/// </remarks>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The client IP address the API sees for a request, so a test can stay within
    /// one rate limit window. Without it, every request gets a new address, so
    /// ordinary tests never hit the calculate rate limit.
    /// </summary>
    public const string ClientIpHeader = "X-Test-Client-Ip";

    private readonly string _connectionString =
        $@"Server=(localdb)\MSSQLLocalDB;Database=JeepneyFareCalculator_Tests_{Guid.NewGuid():N};" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>The test run's one Administrator. Sign in with these through <c>/api/admin/login</c>.</summary>
    public const string AdministratorUsername = "test-admin";
    public const string AdministratorPassword = "correct-test-password";

    public FakeDistanceEstimator DistanceEstimator { get; } = new();

    /// <summary>
    /// Creates a client that talks to the API over https. Use it for anything
    /// that signs in: the authentication cookie is Secure, so the client only
    /// sends it back over https.
    /// </summary>
    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    /// <summary>
    /// Creates an https client that is signed in as the Administrator through
    /// the real login endpoint. Its cookie is sent with every later request.
    /// </summary>
    public async Task<HttpClient> CreateSignedInClientAsync()
    {
        var client = CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            username = AdministratorUsername,
            password = AdministratorPassword,
        });
        response.EnsureSuccessStatusCode();
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _connectionString);
        builder.UseSetting("Administrator:Username", AdministratorUsername);
        builder.UseSetting("Administrator:PasswordHash", PasswordHashing.Hash(AdministratorPassword));

        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, TestClientIpStartupFilter>();
            UseFakeDistanceEstimator(services);
        });
    }

    /// <summary>Puts <see cref="DistanceEstimator"/> in place of the real distance estimator.</summary>
    protected virtual void UseFakeDistanceEstimator(IServiceCollection services)
    {
        services.RemoveAll<IDistanceEstimator>();
        services.AddSingleton<IDistanceEstimator>(DistanceEstimator);
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>
    /// In-memory requests have no client IP address. This sets one first thing in
    /// the pipeline, from <see cref="ClientIpHeader"/> or else a new one per request.
    /// </summary>
    private sealed class TestClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress =
                        IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var clientIp)
                            ? clientIp
                            : new IPAddress(Guid.NewGuid().ToByteArray());
                    return nextMiddleware(context);
                });
                next(app);
            };
        }
    }
}
