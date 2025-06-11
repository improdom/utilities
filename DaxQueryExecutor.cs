protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    const int apiPollingIntervalMs = 10 * 1000;         // 10 seconds
    const int orchestratorPollingIntervalMs = 2 * 60 * 1000; // 2 minutes

    using var scope = _scopeFactory.CreateScope();
    _readinessTracker = scope.ServiceProvider.GetRequiredService<ExecutionReadinessTracker>();

    _logger.LogInformation("SubDeskReadinessWorker started.");

    DateTime lastOrchestratorCheck = DateTime.UtcNow;
    List<Node> accumulatedOrchestratorNodes = new();

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Step 1: Call orchestrator every 2 minutes
            if ((now - lastOrchestratorCheck).TotalMilliseconds >= orchestratorPollingIntervalMs)
            {
                DateTime fromRange = lastOrchestratorCheck;
                DateTime toRange = now;
                lastOrchestratorCheck = toRange;

                var orchestratorReadyNodes = await _dataReadinessService
                    .GetNodeReadinessFromOrchestrator(fromRange, toRange);

                if (orchestratorReadyNodes.Any())
                {
                    accumulatedOrchestratorNodes.AddRange(orchestratorReadyNodes);
                }
            }

            // Step 2: Poll API every 10 seconds
            var apiReadyNodes = _eventQueue.DequeueAll();

            var allReadyNodes = accumulatedOrchestratorNodes.Union(apiReadyNodes).ToList();

            if (allReadyNodes.Any())
            {
                var currentCoB = allReadyNodes.Max(x => x.BusinessDate);
                _logger.LogInformation("Received readiness event for sub-desk: {SubDesk}", allReadyNodes);
                await _readinessTracker.MarkNodeAsReady(allReadyNodes, currentCoB);

                // Clear processed orchestrator nodes
                accumulatedOrchestratorNodes.Clear();
            }

            await Task.Delay(apiPollingIntervalMs, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SubDeskReadinessWorker.");
            await Task.Delay(apiPollingIntervalMs, stoppingToken); // prevent tight loop on error
        }
    }
}
