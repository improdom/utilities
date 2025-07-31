public async Task ExecuteRefreshCycleAsync(List<EventSet> eventSets)
{
    int workerCount = await _modelRepo.GetWorkerModelsCountAsync();
    _logger.LogInformation("🔄 Starting model refresh cycle for {WorkerCount} worker(s)...", workerCount);

    var mergedEventSet = EventSet.MergeEventSets(eventSets.ToList());

    for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
    {
        _logger.LogInformation("➡️  Refreshing worker model {Index} of {Total}...", workerIndex + 1, workerCount);

        var refreshResult = await SwitchAndRefreshAsync(mergedEventSet);

        if (refreshResult.Status == RefreshStatus.FAILED)
        {
            if (workerIndex == 0)
            {
                // ❌ First model failed — abort full cycle
                _logger.LogError("🚫 First worker model refresh failed. Aborting entire cycle.");
                await _modelRepo.LogRefreshFailureAsync(mergedEventSet, "First worker model refresh failed. Full cycle aborted.");
                return;
            }

            // 🔁 Retry once for subsequent workers
            _logger.LogWarning("⚠️  Worker model {Index} refresh failed. Retrying once...", workerIndex + 1);
            refreshResult = await SwitchAndRefreshAsync(mergedEventSet);

            if (refreshResult.Status == RefreshStatus.FAILED)
            {
                _logger.LogError("❌ Retry failed for worker model {Index}. Cycle paused until issue is resolved.", workerIndex + 1);
                await _modelRepo.LogRefreshFailureAsync(mergedEventSet, $"Worker model {workerIndex + 1} failed after retry. Cycle halted.");
                return;
            }
            else
            {
                _logger.LogInformation("✅ Retry succeeded for worker model {Index}. Continuing cycle...", workerIndex + 1);
            }
        }
        else
        {
            _logger.LogInformation("✅ Refresh succeeded for worker model {Index}.", workerIndex + 1);
        }
    }

    _logger.LogInformation("🎉 Refresh cycle completed successfully for all {Count} workers.", workerCount);
}


private void LogCycleStep(string message, params object[] args)
{
    _logger.LogInformation("🔧 [Cycle] " + message, args);
}
