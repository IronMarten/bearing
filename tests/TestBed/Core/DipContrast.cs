namespace TestBed.Core;

// A controlled pair: identical shape, opposite dependency style.
// ConcreteConsumer should read fragile; AbstractConsumer should read insulated.

public interface IAuditSink { void Write(string message); }
public interface IClock { DateTime UtcNow { get; } }
public interface IRetryPolicy { bool ShouldRetry(int attempt); }
public interface IFeatureGate { bool IsEnabled(string flag); }

public class AuditSink : IAuditSink { public void Write(string message) { } }
public class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
public class RetryPolicy : IRetryPolicy { public bool ShouldRetry(int attempt) => attempt < 3; }
public class FeatureGate : IFeatureGate { public bool IsEnabled(string flag) => flag != null; }

// Depends on four CONCRETE services.
public class ConcreteConsumer
{
    private readonly AuditSink _audit = new();
    private readonly SystemClock _clock = new();
    private readonly RetryPolicy _retry = new();
    private readonly FeatureGate _gate = new();

    public bool Process(int attempt, string flag)
    {
        _audit.Write($"{_clock.UtcNow}");
        return _gate.IsEnabled(flag) && _retry.ShouldRetry(attempt);
    }
}

// Depends on four ABSTRACTIONS. Same work, inverted dependencies.
public class AbstractConsumer
{
    private readonly IAuditSink _audit;
    private readonly IClock _clock;
    private readonly IRetryPolicy _retry;
    private readonly IFeatureGate _gate;

    public AbstractConsumer(IAuditSink audit, IClock clock, IRetryPolicy retry, IFeatureGate gate)
    {
        _audit = audit; _clock = clock; _retry = retry; _gate = gate;
    }

    public bool Process(int attempt, string flag)
    {
        _audit.Write($"{_clock.UtcNow}");
        return _gate.IsEnabled(flag) && _retry.ShouldRetry(attempt);
    }
}

// Gives both consumers equal fan-in so only the dependency style differs.
public class ConsumerFacade
{
    private readonly ConcreteConsumer _concrete = new();
    private readonly AbstractConsumer _abstract = new(null, null, null, null);
    public bool Run(int n) => _concrete.Process(n, "x") || _abstract.Process(n, "x");
}
