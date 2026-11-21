using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class CustomerSpecificReleaseTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("customer-001", "new")]
    [InlineData("customer-002", "legacy")]
    public async Task Customer_release_routes_payments_by_allowlist(string customer, string expected)
    {
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        await AssertPaymentAsync(output, client, $"Customer-specific release for {customer}",
            "/customer-release", expected, customer);
    }

    [Fact]
    public Task Missing_customer_identity_returns_bad_request()
        => AssertMissingCustomerAsync(output, "/customer-release", Payment());
}
