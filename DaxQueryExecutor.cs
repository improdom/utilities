using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class RunnerHostedService : BackgroundService
{
    private readonly Runner _runner;
    private readonly ILogger<RunnerHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public RunnerHostedService(
        Runner runner,
        ILogger<RunnerHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _runner = runner;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Deployment runner starting...");
            await _runner.RunAsync(stoppingToken);   // Prefer async if possible
            _logger.LogInformation("Deployment runner finished successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment runner failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            // Important: stop the host so the process exits
            _lifetime.StopApplication();
        }
    }
}
