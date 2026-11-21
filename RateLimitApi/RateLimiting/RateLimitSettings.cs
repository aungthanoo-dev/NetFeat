namespace RateLimitApi.RateLimiting;

internal sealed class RateLimitSettings
{
    public StatusWindow StatusPolicy { get; init; } = StatusWindow.FixedWindow;
    public int SubmissionTokenLimit { get; init; } = 5;
    public int SubmissionTokensPerPeriod { get; init; } = 1;
    public TimeSpan SubmissionReplenishmentPeriod { get; init; } = TimeSpan.FromSeconds(2);
    public int StatusPermitLimit { get; init; } = 10;
    public TimeSpan StatusWindowLength { get; init; } = TimeSpan.FromSeconds(10);
    public int StatusSegmentsPerWindow { get; init; } = 5;
    public int ExportPermitLimit { get; init; } = 2;
}

internal enum StatusWindow
{
    FixedWindow,
    SlidingWindow
}
