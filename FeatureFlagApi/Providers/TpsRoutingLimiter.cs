namespace FeatureFlagApi.Providers;

// One shared limit per API process, measured over the preceding second.
public sealed class TpsRoutingLimiter
{
    private readonly object _gate = new();
    private readonly Queue<long> _admissions = new();
    private readonly TimeProvider _clock;

    public int Limit { get; }

    public TpsRoutingLimiter(IConfiguration configuration, TimeProvider clock)
    {
        _clock = clock;
        Limit = configuration.GetValue<int>("TpsRouting:NewProviderLimitPerSecond", 20);
        if (Limit <= 0)
        {
            throw new InvalidOperationException("TpsRouting:NewProviderLimitPerSecond must be positive.");
        }
    }

    public bool TryAcquire()
    {
        lock (_gate)
        {
            var now = _clock.GetTimestamp();
            while (_admissions.TryPeek(out var oldest) &&
                   _clock.GetElapsedTime(oldest, now) >= TimeSpan.FromSeconds(1))
            {
                _admissions.Dequeue();
            }

            if (_admissions.Count >= Limit)
            {
                return false;
            }

            _admissions.Enqueue(now);
            return true;
        }
    }
}
