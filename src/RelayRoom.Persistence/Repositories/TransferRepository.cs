using Microsoft.EntityFrameworkCore;
using RelayRoom.Application.Persistence;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence.Repositories;

public sealed class TransferRepository(AppDbContext context) : Repository<Transfer>(context), ITransferRepository
{
    public Task<Transfer?> GetWithRoomAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.Include(t => t.Room).ThenInclude(r => r.Devices)
            .Include(t => t.Blob)
            .Include(t => t.Deliveries)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Transfer?> GetByIdUntrackedAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
