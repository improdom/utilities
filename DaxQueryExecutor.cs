protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var environment = "staging";
    var mrvEnvironment = ConfigurationHelper.GetEnvironments()
        .FirstOrDefault(x => x.Name == environment);
    var mrvRepo = new MrvRepository(new ConfigHelper(), mrvEnvironment);

    // Define the scheduled run
    var scheduledDay = DayOfWeek.Friday;
    var scheduledTime = new TimeSpan(18, 0, 0); // 6 PM UTC
    DateTime? lastScheduledRun = null;

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            // 🛠 Regular processing logic
            var startTime = DateTime.UtcNow;
            var latestVersion = mrvRepo.GetBuilderVersion();

            if (!string.IsNullOrWhiteSpace(latestVersion))
            {
                RunMrvBuilder(mrvEnvironment);
                mrvRepo.UpdateMrvVersionControl(latestVersion, start: startTime, end: DateTime.UtcNow);
            }

            // ⏰ Scheduled Task Check
            var now = DateTime.UtcNow;
            var isScheduledDay = now.DayOfWeek == scheduledDay;
            var isScheduledTime = now.TimeOfDay >= scheduledTime;
            var hasNotRunYet = lastScheduledRun == null || lastScheduledRun.Value.Date != now.Date;

            if (isScheduledDay && isScheduledTime && hasNotRunYet)
            {
                _logger.LogInformation("Running scheduled Postgres task...");
                await CallPostgresStoredProcedure();
                lastScheduledRun = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in combined background service logic.");
        }

        await Task.Delay(1000, stoppingToken); // Adjustable frequency
    }
}
