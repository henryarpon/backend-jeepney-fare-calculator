using System.Net.Http.Json;
using JeepneyFareCalculator.Application.Administrators;
using JeepneyFareCalculator.Application.Distances;
using JeepneyFareCalculator.Infrastructure;
using JeepneyFareCalculator.Infrastructure.Persistence;
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
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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
            services.RemoveAll<IDistanceEstimator>();
            services.AddSingleton<IDistanceEstimator>(DistanceEstimator);
        });
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
}
