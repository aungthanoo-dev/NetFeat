using System.Net;
using System.Net.Http.Json;
using RateLimitApi.Dtos;
using Xunit.Abstractions;
using static RateLimitApi.IntegrationTests.PayoutApi;

namespace RateLimitApi.IntegrationTests;

public sealed class TokenBucketTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Payout_burst_is_throttled_per_merchant_and_recovers_after_refill()
    {
        output.WriteLine("Expected   Five accepted payouts, then HTTP 429 for Merchant A, with Merchant B and status polling available.");
        using var api = new PayoutApi();
        using var client = api.CreateDemoClient();

        Uri? location = null;
        for (var index = 0; index < 5; index++)
        {
            using var accepted = await PostAsync(client);
            await AssertAcceptedAsync(accepted);
            location = accepted.Headers.Location;
        }

        using var throttled = await PostAsync(client);
        await AssertThrottledAsync(throttled, output);

        using var otherMerchant = await PostAsync(client, MerchantB);
        await AssertAcceptedAsync(otherMerchant);

        using var status = await GetStatusAsync(client, location!);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        await WaitForRecoveryAsync(token => PostAsync(client, cancellationToken: token),
            HttpStatusCode.Accepted, TimeSpan.FromSeconds(5), output);
    }

    [Fact]
    public async Task Invalid_merchant_headers_do_not_consume_valid_merchant_quota()
    {
        output.WriteLine("Expected   Invalid merchant headers return HTTP 400 and the valid merchant retains its permit.");
        using var api = new PayoutApi(new()
        {
            ["SubmissionTokenLimit"] = "1",
            ["SubmissionReplenishmentPeriod"] = "00:01:00"
        });
        using var client = api.CreateDemoClient();

        foreach (var merchant in new[] { null, "merchant-unknown" })
        {
            using var invalid = await PostAsync(client, merchant);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        using var accepted = await PostAsync(client);
        await AssertAcceptedAsync(accepted);

        using var throttled = await PostAsync(client);
        await AssertThrottledAsync(throttled, output);
    }

    [Fact]
    public async Task Payout_status_is_visible_only_to_the_owner()
    {
        using var api = new PayoutApi();
        using var client = api.CreateDemoClient();

        using var invalid = await PostAsync(client, amountInCents: 0);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var accepted = await PostAsync(client);
        var payout = await AssertAcceptedAsync(accepted);
        Assert.Equal(2599, payout.AmountInCents);
        Assert.Equal("USD", payout.Currency);

        using var owner = await GetStatusAsync(client, accepted.Headers.Location!);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(payout, await owner.Content.ReadFromJsonAsync<PayoutResponse>());

        using var otherMerchant = await GetStatusAsync(client, accepted.Headers.Location!, MerchantB);
        Assert.Equal(HttpStatusCode.NotFound, otherMerchant.StatusCode);
    }
}
