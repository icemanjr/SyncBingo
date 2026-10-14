using System.Net;
using SyncBingo.Api.IntegrationTests.Http;
using SyncBingo.Api.IntegrationTests.Redis;

namespace SyncBingo.Api.IntegrationTests;

public sealed class HealthEndpointTests(RedisFixture redis)
{
    [Fact]
    public async Task Healthz_returns_healthy()
    {
        await using var factory = new ApiFactory(redis);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }
}
