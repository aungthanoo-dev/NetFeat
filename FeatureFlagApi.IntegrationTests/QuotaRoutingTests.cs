using FeatureFlagApi.Dtos;
using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class QuotaRoutingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Quota_routes_first_50_customers_to_new_provider()
    {
        output.Start("Quota routing with 100 customers and 50 slots for new",
            "50 new, 50 legacy. Assign the first 50 to new and preserve customer order.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        using var response = await PostAsync(client, "/quota-routing-preview",
            body: new QuotaRoutingPreviewRequest(Customers, 50));
        var result = await ReadOkAsync<QuotaPreview>(response, output);

        var customerIds = result.Assignments.Select(route => route.CustomerId).ToArray();
        output.Actual($"{result.NewProvider} new, {result.Legacy} legacy, {result.Total} total. " +
            $"First 50 assigned to new: {result.Assignments.Take(50).Count(route => route.Provider == "new")}. " +
            $"Customer order preserved: {Customers.SequenceEqual(customerIds)}.");
        Assert.Equal(100, result.Total);
        Assert.Equal(50, result.NewProvider);
        Assert.Equal(50, result.Legacy);
        Assert.Equal(Customers, customerIds);
        Assert.All(result.Assignments.Take(50), route => Assert.Equal("new", route.Provider));
        Assert.All(result.Assignments.Skip(50), route => Assert.Equal("legacy", route.Provider));
    }

    private sealed record Assignment(string CustomerId, string Provider);
    private sealed record QuotaPreview(int Total, int NewProvider, int Legacy, Assignment[] Assignments);
}
