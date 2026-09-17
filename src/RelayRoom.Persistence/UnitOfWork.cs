using Microsoft.EntityFrameworkCore;
using RelayRoom.Application.Persistence;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence;

public sealed class UnitOfWork(
    AppDbContext context,
    IRoomRepository rooms,
    ITransferRepository transfers,
    IRepository<Device> devices,
    IRepository<BlobObject> blobs,
    IRepository<TransferDelivery> deliveries,
    IRepository<RoomEvent> roomEvents) : IUnitOfWork
{
    public IRoomRepository Rooms { get; } = rooms;
    public ITransferRepository Transfers { get; } = transfers;
    public IRepository<Device> Devices { get; } = devices;
    public IRepository<BlobObject> Blobs { get; } = blobs;
    public IRepository<TransferDelivery> Deliveries { get; } = deliveries;
    public IRepository<RoomEvent> RoomEvents { get; } = roomEvents;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
