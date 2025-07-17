public class UtcScheduler
{
    private readonly Func<CancellationToken, Task> _taskToRun;
    private readonly DayOfWeek _dayOfWeek;
    private readonly TimeSpan _timeOfDayUtc;

    public UtcScheduler(DayOfWeek dayOfWeek, TimeSpan timeOfDayUtc, Func<CancellationToken, Task> taskToRun)
    {
        _dayOfWeek = dayOfWeek;
        _timeOfDayUtc = timeOfDayUtc;
        _taskToRun = taskToRun ?? throw new ArgumentNullException(nameof(taskToRun));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = GetNextRunTime(now);
            var delay = nextRun - now;

            if (delay.TotalMilliseconds > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                await _taskToRun(cancellationToken);
            }
            catch (Exception ex)
            {
                // Consider logging or rethrowing depending on your needs
                Console.WriteLine($"Scheduled task error: {ex.Message}");
            }
        }
    }

    private DateTime GetNextRunTime(DateTime from)
    {
        int daysUntil = ((int)_dayOfWeek - (int)from.DayOfWeek + 7) % 7;
        var next = from.Date.AddDays(daysUntil).Add(_timeOfDayUtc);

        // If the calculated time is already past for today, schedule next week
        if (next <= from)
        {
            next = next.AddDays(7);
        }

        return next;
    }
}




protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Start the scheduler in the background
    var schedulerTask = new UtcScheduler(
        dayOfWeek: DayOfWeek.Friday,
        timeOfDayUtc: new TimeSpan(18, 0, 0),
        taskToRun: async token =>
        {
            _logger.LogInformation("Scheduled task started.");
            await CallPostgresStoredProcedure();
        }).RunAsync(stoppingToken);

    // Start your existing loop logic
    var loopTask = Task.Run(async () =>
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var latestVersion = mrvRepo.GetBuilderVersion();

                if (!string.IsNullOrWhiteSpace(latestVersion))
                {
                    RunMrvBuilder(mrvEnvironment);
                    mrvRepo.UpdateMrvVersionControl(latestVersion, start: startTime, end: DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing MRV Builder.");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }, stoppingToken);

    // Wait for both to complete
    await Task.WhenAll(schedulerTask, loopTask);
}
