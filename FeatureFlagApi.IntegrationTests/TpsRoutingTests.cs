using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class TpsRoutingTests(ITestOutputHelper output)
{
    [Fact]
    public Task Sequential_previews_overflow_to_legacy_and_recover_after_one_second()
        => TpsRoutingScenario.AssertOverflowAndRecoveryAsync(output, concurrent: false);

    [Fact]
    public Task Missing_customer_identity_returns_bad_request()
        => AssertMissingCustomerAsync(output, "/tps-routing-preview");
}
