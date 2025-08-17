using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class ScheduledAction
{
    private readonly TimeSpan _interval;
    private DateTime _nextRunUtc;

    /// <summary>
    /// Initializes a scheduler that will run every <paramref name="interval"/>.
    /// </summary>
    public ScheduledAction(TimeSpan interval)
    {
        _interval = interval;
        _nextRunUtc = DateTime.UtcNow + _interval;
    }

    /// <summary>
    /// Checks if it’s time, and if yes executes <paramref name="action"/>.
    /// </summary>
    public async Task TickAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now < _nextRunUtc) return;

        try
        {
            await action(ct).ConfigureAwait(false);
        }
        finally
        {
            // Schedule next run
            _nextRunUtc = now + _interval;
        }
    }
}
