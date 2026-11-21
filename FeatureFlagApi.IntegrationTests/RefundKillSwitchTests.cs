using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class RefundKillSwitchTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Third_refund_trips_kill_switch_and_later_requests_stay_blocked()
    {
        output.Start("Five consecutive refunds with the refund flag enabled",
            "HTTP sequence 200, 200, 503, 503, 503. Attempt 3 trips the switch and attempts 4 and 5 are blocked.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        var paymentId = Guid.NewGuid();
        var path = $"/{paymentId}/refund";

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var response = await PostAsync(client, path);
            output.Actual($"Refund attempt {attempt}: HTTP {(int)response.StatusCode}.");
            var result = await ReadOkAsync<Refund>(response);
            output.Actual($"Refund attempt {attempt} outcome: {result.Status}.");
            Assert.Equal("succeeded", result.Status);
            Assert.Equal(paymentId, result.PaymentId);
            Assert.NotEqual(Guid.Empty, result.RefundId);
        }

        using var failure = await PostAsync(client, path);
        output.Actual($"Refund attempt 3: HTTP {(int)failure.StatusCode}.");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        var problem = await failure.Content.ReadFromJsonAsync<ProblemDetails>();
        output.Actual($"Refund attempt 3 outcome: {problem?.Title}.");
        Assert.Equal("Refund provider failed; refunds have been paused", problem?.Title);

        for (var attempt = 4; attempt <= 5; attempt++)
        {
            using var response = await PostAsync(client, path);
            output.Actual($"Refund attempt {attempt}: HTTP {(int)response.StatusCode}.");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var blocked = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            output.Actual($"Refund attempt {attempt} outcome: {blocked?.Title}.");
            Assert.Equal("Refunds are temporarily unavailable", blocked?.Title);
        }
    }

    [Fact]
    public async Task Disabled_refund_flag_blocks_first_request()
    {
        output.Start("First refund with the refund flag disabled", "HTTP 503 Service Unavailable.");
        using var api = new PaymentApi("RefundsEnabled", new() { ["enabled"] = "false" });
        using var client = api.CreateClient();
        using var response = await PostAsync(client, $"/{Guid.NewGuid()}/refund");
        output.Actual($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed record Refund(Guid PaymentId, Guid RefundId, string Status);
}
