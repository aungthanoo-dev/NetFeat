namespace FeatureFlagApi.Providers;

// Demo-only, process-local latch. Restart the API to clear its state.
public sealed class RefundKillSwitch(IConfiguration configuration)
{
    private readonly object _gate = new();
    private readonly int _failOnRequest = configuration.GetValue<int>("RefundDemo:FailOnRequest");
    private int _attempts;
    private bool _tripped;

    public bool TryBeginRefund()
    {
        lock (_gate)
        {
            if (_tripped) return false;

            if (_failOnRequest > 0 && ++_attempts == _failOnRequest)
            {
                _tripped = true;
                throw new SimulatedRefundException("Simulated refund provider failure; refund kill switch tripped.");
            }

            return true;
        }
    }
}

public sealed class SimulatedRefundException(string message) : Exception(message);
