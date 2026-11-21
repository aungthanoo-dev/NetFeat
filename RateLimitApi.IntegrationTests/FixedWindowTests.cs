using System.Net;
using Xunit.Abstractions;
using static RateLimitApi.IntegrationTests.PayoutApi;

namespace RateLimitApi.IntegrationTests;

public sealed class FixedWindowTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Status_quota_covers_all_payouts_and_fully_resets_at_the_next_window()
    {
        output.WriteLine("Expected   Ten successful status checks across two payouts, then HTTP 429 until the whole allowance resets.");
        using var api = new PayoutApi(new() { ["StatusWindowLength"] = "00:00:03" });
        using var client = api.CreateDemoClient();
        using var firstPayout = await PostAsync(client);
        using var secondPayout = await PostAsync(client);
        await AssertAcceptedAsync(firstPayout);
        await AssertAcceptedAsync(secondPayout);
        var first = firstPayout.Headers.Location!;
        var second = secondPayout.Headers.Location!;

        for (var index = 0; index < 10; index++)
        {
            using var status = await GetStatusAsync(client, index % 2 == 0 ? first : second);
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        }

        using var throttled = await GetStatusAsync(client, first);
        await AssertThrottledAsync(throttled, output);

        await WaitForRecoveryAsync(token => GetStatusAsync(client, first, cancellationToken: token),
            HttpStatusCode.OK, TimeSpan.FromSeconds(6), output);

        for (var index = 0; index < 9; index++)
        {
            using var status = await GetStatusAsync(client, second);
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        }

        using var nextWindowThrottled = await GetStatusAsync(client, first);
        await AssertThrottledAsync(nextWindowThrottled, output);
    }
}
