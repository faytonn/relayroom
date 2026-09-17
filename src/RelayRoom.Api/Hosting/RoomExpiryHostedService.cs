using RelayRoom.Application.Services;
using RelayRoom.Core.Errors;

namespace RelayRoom.Api.Hosting;

public sealed class RoomExpiryHostedService(IServiceScopeFactory scopes, ILogger<RoomExpiryHostedService> logger) : BackgroundService
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
                    logger.LogInformation("Room cleanup warned={Warned} expired={Expired} purged={Purged}", result.Warned, result.Expired, result.Purged);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Room expiry loop failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class ExceptionHandling
{
    public static async Task Write(HttpContext context, Exception exception)
    {
        if (exception is RelayRoomException relay)
        {
            context.Response.StatusCode = relay.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = relay.Message });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        await context.Response.WriteAsJsonAsync(new
        {
            error = env.IsDevelopment() ? exception.Message : "Unexpected error.",
            detail = env.IsDevelopment() ? exception.ToString() : null
        });
    }
}
