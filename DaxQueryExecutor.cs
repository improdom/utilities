private async Task<bool> ShouldRunWeekdayModelPurgeAsync()
{
    var now = DateTime.UtcNow;
    var scheduledTime = new TimeSpan(6, 0, 0); // Only after 6 AM UTC

    // ✅ Skip weekends
    if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
        return false;

    // ✅ Ensure it's *after* the scheduled time (not exactly equal)
    if (now.TimeOfDay < scheduledTime)
        return false;

    // ✅ Get current writer model
    var state = await modelRepo.GetCurrentModelStateAsync();
    var writerModel = await modelRepo.GetModelByIdAsync(state.WriterModelId);

    // ✅ Only run if it hasn't already run today
    var hasNotRunYet = writerModel.PurgedAt == null || writerModel.PurgedAt.Value.Date != now.Date;
    return hasNotRunYet;
}
