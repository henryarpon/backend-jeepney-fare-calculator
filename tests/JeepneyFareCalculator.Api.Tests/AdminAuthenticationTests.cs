using System.Net;
using System.Net.Http.Json;
using JeepneyFareCalculator.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AdminAuthenticationTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_CorrectCredentials_SetsHttpOnlyAuthenticationCookie()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            username = ApiFactory.AdministratorUsername,
            password = ApiFactory.AdministratorPassword,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ApiFactory.AdministratorUsername, "wrong-password")]
    [InlineData("someone-else", ApiFactory.AdministratorPassword)]
    public async Task Login_WrongCredentials_Returns401AsProblemDetailsAndSetsNoCookie(string username, string password)
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/admin/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem?.Status);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Me_SignedIn_Returns200WithUsername()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.GetAsync("/api/admin/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var administrator = await response.Content.ReadFromJsonAsync<SignedInAdministratorBody>();
        Assert.Equal(ApiFactory.AdministratorUsername, administrator?.Username);
    }

    [Fact]
    public async Task Me_SignedOut_Returns401AsProblemDetails()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/admin/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Logout_SignedIn_MeReturns401Afterwards()
    {
        var client = await factory.CreateSignedInClientAsync();

        var logoutResponse = await client.PostAsync("/api/admin/logout", content: null);
        var meResponse = await client.GetAsync("/api/admin/me");

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_SignedIn_OldCookieNoLongerWorks()
    {
        var client = factory.CreateHttpsClient();
        var loginResponse = await client.PostAsJsonAsync("/api/admin/login", new
        {
            username = ApiFactory.AdministratorUsername,
            password = ApiFactory.AdministratorPassword,
        });
        var oldCookie = loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        await client.PostAsync("/api/admin/logout", content: null);

        // A client that ignores the logout response and replays the old cookie.
        var replayingClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/me");
        request.Headers.Add("Cookie", oldCookie);
        var meResponse = await replayingClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    private sealed record SignedInAdministratorBody(string Username);
}
