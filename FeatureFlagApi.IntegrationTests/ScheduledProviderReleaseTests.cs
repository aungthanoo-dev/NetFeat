using System.Net;
using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class ScheduledProviderReleaseTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, 2, true, "legacy")]
    [InlineData(-1, 1, true, "new")]
    [InlineData(-2, -1, true, "legacy")]
    [InlineData(-1, 1, false, "legacy")]
    public async Task Scheduled_release_obeys_window_and_flag(
        int startHours, int endHours, bool enabled, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        using var api = new PaymentApi("ScheduledPaymentProviderRelease", new()
        {
            ["enabled"] = enabled.ToString(),
            ["conditions:client_filters:0:parameters:Start"] = now.AddHours(startHours).ToString("O"),
            ["conditions:client_filters:0:parameters:End"] = now.AddHours(endHours).ToString("O")
        });
        using var client = api.CreateClient();
        await AssertPaymentAsync(output, client,
            $"Scheduled release with flag enabled={enabled} and window from {startHours:+0;-0} h to {endHours:+0;-0} h",
            "/time-window-release", expected);
    }

    [Fact]
    public async Task Scheduled_release_rejects_invalid_payment()
    {
        output.Start("Scheduled release payment with amount 0", "HTTP 400 Bad Request.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        using var response = await PostAsync(client, "/time-window-release", body: Payment("invalid", 0));
        output.Actual($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
