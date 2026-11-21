using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class CombinedAndFiltersTests(ITestOutputHelper output)
{
    private const string Path = "/combined-filter-release";
    private const string FlagId = "CombinedPaymentProviderRelease";

    [Theory]
    [InlineData("customer-001", true, 100, "new")]
    [InlineData("customer-001", true, 0, "legacy")]
    [InlineData("customer-001", false, 100, "legacy")]
    [InlineData("customer-002", true, 100, "legacy")]
    public async Task Filters_select_expected_provider(string customer, bool active, int percentage, string expected)
    {
        using var api = FilterScenarios.CreateApi(FlagId, active, percentage);
        using var client = api.CreateClient();
        await AssertPaymentAsync(output, client,
            $"AND filters for {customer}, window active={active}, sampling={percentage}%", Path, expected, customer);
    }

    [Fact]
    public Task Sample_returns_100_valid_payments()
        => FilterScenarios.AssertSampleAsync(output, FlagId, Path, "customer-001", active: true);

    [Fact]
    public Task Missing_customer_identity_returns_bad_request()
        => AssertMissingCustomerAsync(output, Path, Payment());
}
