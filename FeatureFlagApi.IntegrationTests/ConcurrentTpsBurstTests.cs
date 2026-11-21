using Xunit.Abstractions;

namespace FeatureFlagApi.IntegrationTests;

public sealed class ConcurrentTpsBurstTests(ITestOutputHelper output)
{
    [Fact]
    public Task Concurrent_previews_overflow_to_legacy_and_recover_after_one_second()
        => TpsRoutingScenario.AssertOverflowAndRecoveryAsync(output, concurrent: true);
}
