using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

internal static class FilterScenarios
{
    internal static async Task AssertSampleAsync(
        ITestOutputHelper output, string flagId, string path, string customer, bool active)
    {
        var window = active ? "inside" : "outside";
        output.Start($"100 filter evaluations for {customer} {window} the release window",
            "100 successful payments, each assigned to new or legacy.");
        using var api = CreateApi(flagId, active, 20);
        using var client = api.CreateClient();

        var providers = await Task.WhenAll(Enumerable.Range(1, 100)
            .Select(index => PayAsync(client, path, customer, $"filter-{index}", output, reportSuccess: false)));
        var newCount = providers.Count(provider => provider == "new");
        output.Actual($"{providers.Length} successful payments, {newCount} new, " +
            $"{providers.Length - newCount} legacy ({(double)newCount / providers.Length:P1} new).");
        output.Details("Configured sampling probability: 20% per eligible evaluation.");
    }

    internal static PaymentApi CreateApi(string flagId, bool active, int percentage)
    {
        var now = DateTimeOffset.UtcNow;
        // Keep each window within its anchor date by putting local noon at start.
        var start = now.AddMinutes(active ? -15 : 60);
        var offset = TimeSpan.FromHours(12) - TimeSpan.FromHours(start.Hour);
        start = start.ToOffset(offset);
        return new PaymentApi(flagId, new()
        {
            ["conditions:client_filters:1:parameters:Start"] = start.ToString("O"),
            ["conditions:client_filters:1:parameters:End"] = start.AddHours(2).ToString("O"),
            ["conditions:client_filters:2:parameters:Value"] = percentage.ToString()
        });
    }
}
