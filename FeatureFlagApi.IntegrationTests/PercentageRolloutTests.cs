using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class PercentageRolloutTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Percentage_rollout_assigns_both_providers_and_is_stable_for_each_customer()
    {
        output.Start($"Percentage rollout for {Customers.Length} customers, previewed twice",
            $"Both providers are represented and all {Customers.Length} customers keep their first assignment.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();

        var first = await Task.WhenAll(Customers.Select(customer => PreviewAsync(customer)));
        var second = await Task.WhenAll(Customers.Select(customer => PreviewAsync(customer)));

        var newCount = first.Count(route => route.Provider == "new");
        output.Actual($"{newCount} new, {first.Length - newCount} legacy " +
            $"({(double)newCount / first.Length:P1} new).");
        output.Actual($"Repeat preview matched for {first.Zip(second).Count(pair => pair.First == pair.Second)} of {first.Length} customers.");
        output.Details("Configured rollout target: 50%.");
        Assert.Contains(first, route => route.Provider == "new");
        Assert.Contains(first, route => route.Provider == "legacy");
        Assert.Equal(first, second);

        async Task<RoutingPreview> PreviewAsync(string customer)
        {
            using var response = await PostAsync(client, "/routing-preview", customer);
            var result = await ReadOkAsync<RoutingPreview>(response, output);
            Assert.Equal(customer, result.CustomerId);
            Assert.Contains(result.Provider, new[] { "new", "legacy" });
            return result;
        }
    }

    [Fact]
    public Task Missing_customer_identity_returns_bad_request()
        => AssertMissingCustomerAsync(output, "/routing-preview");

    private sealed record RoutingPreview(string CustomerId, string Provider);
}
