using System.Diagnostics;
using System.Net;
using Xunit.Abstractions;
using static RateLimitApi.IntegrationTests.PayoutApi;

namespace RateLimitApi.IntegrationTests;

public sealed class SlidingWindowTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Only_the_permits_in_the_expired_status_segment_return()
    {
        output.WriteLine("Expected   Two batches in separate segments exhaust the quota. The first expired segment returns two permits.");
        using var api = new PayoutApi(new()
        {
            ["StatusPolicy"] = "SlidingWindow",
            ["StatusPermitLimit"] = "4",
            ["StatusWindowLength"] = "00:00:06",
            ["StatusSegmentsPerWindow"] = "3"
        });
        using var client = api.CreateDemoClient();
        using var payout = await PostAsync(client);
        await AssertAcceptedAsync(payout);
        var location = payout.Headers.Location!;

        var elapsed = Stopwatch.StartNew();
        await ReadTwoStatusesAsync();
        var untilSecondSegment = TimeSpan.FromSeconds(3) - elapsed.Elapsed;
        if (untilSecondSegment > TimeSpan.Zero)
        {
            await Task.Delay(untilSecondSegment);
        }

        await ReadTwoStatusesAsync();
        using var throttled = await GetStatusAsync(client, location);
        await AssertThrottledAsync(throttled, output);

        await WaitForRecoveryAsync(token => GetStatusAsync(client, location, cancellationToken: token),
            HttpStatusCode.OK, TimeSpan.FromSeconds(6), output);

        using var lastRecoveredPermit = await GetStatusAsync(client, location);
        Assert.Equal(HttpStatusCode.OK, lastRecoveredPermit.StatusCode);

        using var stillThrottled = await GetStatusAsync(client, location);
        await AssertThrottledAsync(stillThrottled, output);

        async Task ReadTwoStatusesAsync()
        {
            for (var index = 0; index < 2; index++)
            {
                using var status = await GetStatusAsync(client, location);
                Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            }
        }
    }
}
