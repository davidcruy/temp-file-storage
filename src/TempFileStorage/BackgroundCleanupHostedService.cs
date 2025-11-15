using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TempFileStorage;

internal class BackgroundCleanupHostedService(TimeSpan interval, IServiceScopeFactory factory, ILogger<BackgroundCleanupHostedService> logger) : IHostedService, IAsyncDisposable
{
    private Timer _timer;
    private Task _executingTask;
    private readonly CancellationTokenSource _stoppingCts = new();

    private TimeSpan Interval { get; } = interval;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, Interval);

        logger.LogInformation("{Service} is running", nameof(BackgroundCleanupHostedService));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        // Stop called without start
        if (_executingTask == null)
        {
            return;
        }

        try
        {
            // Signal cancellation to the executing method
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            // Wait until the task completes or the stop token triggers
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private void DoWork(object state)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _executingTask = ExecuteTaskAsync(_stoppingCts.Token);
    }

    private async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Start temp file storage cleanup");

        try
        {
            using var scope = factory.CreateScope();

            var storage = scope.ServiceProvider.GetService<ITempFileStorage>();
            await storage.CleanupStorage(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "BackgroundTask Failed");
        }

        logger.LogDebug("Stop temp file storage cleanup");

        _timer.Change(Interval, TimeSpan.FromMilliseconds(-1));
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer != null) await _timer.DisposeAsync();
    }
}