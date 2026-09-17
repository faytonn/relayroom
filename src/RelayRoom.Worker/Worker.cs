using RelayRoom.Application.Services;

namespace RelayRoom.Worker;

public sealed class Worker(IServiceScopeFactory scopes, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IRoomCleanupService>();
                var result = await cleanup.RunOnceAsync(stoppingToken);
                if (result.Warned + result.Expired + result.Purged > 0)
                {
                    logger.LogInformation(
                        "Cleanup warned={Warned} expired={Expired} purged={Purged}",
                        result.Warned,
                        result.Expired,
                        result.Purged);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cleanup worker failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
