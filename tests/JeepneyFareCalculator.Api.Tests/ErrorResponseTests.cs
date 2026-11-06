using System.Net;
using System.Net.Http.Json;
using JeepneyFareCalculator.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ErrorResponseTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_UnknownRoute_Returns404AsProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }
}
