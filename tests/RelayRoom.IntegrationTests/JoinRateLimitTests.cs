using System.Net;
using System.Net.Http.Json;

namespace RelayRoom.IntegrationTests;

public sealed class JoinRateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly HttpClient _client;

    public JoinRateLimitTests(RateLimitedApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExtraJoinAttempts_Return429()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await _client.PostAsJsonAsync("/api/rooms/join", new { code = "ZZZZZZ" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
