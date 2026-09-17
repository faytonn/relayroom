namespace RelayRoom.Application.Services;

public interface IRoomCleanupService
{
    Task<CleanupResult> RunOnceAsync(CancellationToken cancellationToken);
    Task<int> WarnAndExpireAsync(CancellationToken cancellationToken);
}

public sealed record CleanupResult(int Warned, int Expired, int Purged);
