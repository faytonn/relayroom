using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Persistence;

public interface IUnitOfWork
{
    IRoomRepository Rooms { get; }
    ITransferRepository Transfers { get; }
    IRepository<Device> Devices { get; }
    IRepository<BlobObject> Blobs { get; }
    IRepository<TransferDelivery> Deliveries { get; }
    IRepository<RoomEvent> RoomEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
