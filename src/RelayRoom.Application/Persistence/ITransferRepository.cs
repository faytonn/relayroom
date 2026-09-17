using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Persistence;

public interface ITransferRepository : IRepository<Transfer>
{
    Task<Transfer?> GetWithRoomAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transfer?> GetByIdUntrackedAsync(Guid id, CancellationToken cancellationToken = default);
}
