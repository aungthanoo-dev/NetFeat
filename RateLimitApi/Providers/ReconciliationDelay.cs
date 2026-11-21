namespace RateLimitApi.Providers;

public interface IReconciliationDelay
{
    Task WaitAsync(CancellationToken cancellationToken);
}

internal sealed class ReconciliationDelay : IReconciliationDelay
{
    public Task WaitAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
}
