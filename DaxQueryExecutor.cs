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
