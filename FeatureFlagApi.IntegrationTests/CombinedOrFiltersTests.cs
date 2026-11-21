using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class CombinedOrFiltersTests(ITestOutputHelper output)
{
    private const string Path = "/any-filter-release";
    private const string FlagId = "AnyFilterPaymentProviderRelease";

    [Theory]
    [InlineData("customer-001", false, 0, "new")]
    [InlineData("customer-002", true, 0, "new")]
    [InlineData("customer-002", false, 100, "new")]
    [InlineData("customer-002", false, 0, "legacy")]
    public async Task Filters_select_expected_provider(string customer, bool active, int percentage, string expected)
    {
        using var api = FilterScenarios.CreateApi(FlagId, active, percentage);
        using var client = api.CreateClient();
        await AssertPaymentAsync(output, client,
            $"OR filters for {customer}, window active={active}, sampling={percentage}%", Path, expected, customer);
    }

    [Fact]
    public Task Sample_returns_100_valid_payments()
        => FilterScenarios.AssertSampleAsync(output, FlagId, Path, "customer-002", active: false);

    [Fact]
    public Task Missing_customer_identity_returns_bad_request()
        => AssertMissingCustomerAsync(output, Path, Payment());
}
