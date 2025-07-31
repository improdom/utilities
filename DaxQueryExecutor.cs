public async Task<bool> VerifyWorkerModelHealthAsync()
{
    var workers = await _dbContext.WorkerModels.ToListAsync();

    var unhealthy = workers
        .Where(w => w.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase) ||
                    w.Status.Equals("UNHEALTHY", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (unhealthy.Any())
    {
        _logger.LogWarning("🚫 Refresh cycle aborted. The following worker models are in a FAILED state: {Workers}",
            string.Join(", ", unhealthy.Select(w => w.ModelName ?? w.WorkerModelId.ToString())));

        return false;
    }

    _logger.LogInformation("✅ All worker models are healthy. Proceeding with refresh cycle.");
    return true;
}
