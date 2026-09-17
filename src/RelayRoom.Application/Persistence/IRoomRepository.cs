using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Persistence;

public interface IRoomRepository : IRepository<Room>
{
    Task<Room?> GetActiveByPublicCodeAsync(string publicCode, CancellationToken cancellationToken = default);
    Task<bool> ActiveCodeExistsAsync(string publicCode, CancellationToken cancellationToken = default);
    Task<Room?> GetByIdWithDevicesAndTransfersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Room?> GetGraphByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> ListExpiringAsync(DateTimeOffset now, TimeSpan warningWindow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> ListDueToExpireAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> ListExpiredForPurgeAsync(CancellationToken cancellationToken = default);
}
