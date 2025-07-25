public async Task RunWithThrottleAsync<T>(
    IEnumerable<T> items,
    int maxDegreeOfParallelism,
    Func<T, IServiceProvider, CancellationToken, Task> taskFactory,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken = default)
{
    if (items == null) throw new ArgumentNullException(nameof(items));
    if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
    if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

    using var enumerator = items.GetEnumerator();
    var tasks = new HashSet<Task>();

    // Start initial batch
    while (tasks.Count < maxDegreeOfParallelism && enumerator.MoveNext())
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = RunScopedTask(enumerator.Current, taskFactory, serviceProvider, cancellationToken);
        tasks.Add(task);
    }

    // Process remaining items
    while (tasks.Count > 0)
    {
        var completed = await Task.WhenAny(tasks);
        tasks.Remove(completed);

        try
        {
            await completed; // Observe exceptions
        }
        catch (OperationCanceledException)
        {
            // Cancel immediately and stop starting new tasks
            break;
        }
        catch (Exception ex)
        {
            // Log and continue; or rethrow if you want to halt
            // _logger.LogError(ex, "Task failed.");
        }

        if (enumerator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = RunScopedTask(enumerator.Current, taskFactory, serviceProvider, cancellationToken);
            tasks.Add(task);
        }
    }
}

private async Task RunScopedTask<T>(
    T item,
    Func<T, IServiceProvider, CancellationToken, Task> taskFactory,
    IServiceProvider rootServiceProvider,
    CancellationToken cancellationToken)
{
    using var scope = rootServiceProvider.CreateScope();
    await taskFactory(item, scope.ServiceProvider, cancellationToken);
}

await RunWithThrottleAsync(
    items: myQueryList,
    maxDegreeOfParallelism: 8,
    taskFactory: async (query, scopedProvider, ct) =>
    {
        var dbContext = scopedProvider.GetRequiredService<MyDbContext>();
        var daxService = scopedProvider.GetRequiredService<IDaxQueryExecutionService>();

        var result = await daxService.ExecuteQueryAsync(query, ct);
        if (result.Success)
        {
            dbContext.Results.Add(result);
            await dbContext.SaveChangesAsync(ct);
        }
    },
    serviceProvider: _serviceProvider, // Injected from controller or background service
    cancellationToken: cancellationToken
);

