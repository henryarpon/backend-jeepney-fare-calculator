using System.Net;
using JeepneyFareCalculator.Api.Tests.Infrastructure;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task GetHealth_ApiAndDatabaseAreUp_Returns200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
