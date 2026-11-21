using Xunit.Abstractions;

namespace FeatureFlagApi.IntegrationTests;

internal static class TestOutput
{
    internal static void Start(this ITestOutputHelper output, string scenario, string expected)
    {
        output.WriteLine($"Scenario   {scenario}");
        output.Expected(expected);
    }

    internal static void Expected(this ITestOutputHelper output, string result)
        => output.WriteLine($"Expected   {result}");

    internal static void Actual(this ITestOutputHelper output, string result)
        => output.WriteLine($"Actual     {result}");

    internal static void Details(this ITestOutputHelper output, string detail)
        => output.WriteLine($"Details    {detail}");
}
