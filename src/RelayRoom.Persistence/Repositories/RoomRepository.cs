using Microsoft.EntityFrameworkCore;
using RelayRoom.Application.Persistence;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence.Repositories;

public sealed class RoomRepository(AppDbContext context) : Repository<Room>(context), IRoomRepository
{
    public Task<Room?> GetActiveByPublicCodeAsync(string publicCode, CancellationToken cancellationToken = default) =>
        Set.Include(r => r.Devices)
            .SingleOrDefaultAsync(r => r.PublicCode == publicCode && r.Status == RoomStatus.Active, cancellationToken);

    public Task<bool> ActiveCodeExistsAsync(string publicCode, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(r => r.PublicCode == publicCode && r.Status == RoomStatus.Active, cancellationToken);

    public Task<Room?> GetByIdWithDevicesAndTransfersAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.Include(r => r.Devices)
            .Include(r => r.Transfers)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Room?> GetGraphByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.Include(r => r.Devices)
            .Include(r => r.Transfers).ThenInclude(t => t.Deliveries)
            .Include(r => r.Transfers).ThenInclude(t => t.Blob)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Room>> ListExpiringAsync(DateTimeOffset now, TimeSpan warningWindow, CancellationToken cancellationToken = default) =>
        await Set.Where(r =>
                r.Status == RoomStatus.Active
                && !r.ExpiringWarningSent
                && r.ExpiresAt <= now.Add(warningWindow)
                && r.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Room>> ListDueToExpireAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await Set.Include(r => r.Transfers).ThenInclude(t => t.Blob)
            .Where(r => r.Status == RoomStatus.Active && r.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Room>> ListExpiredForPurgeAsync(CancellationToken cancellationToken = default) =>
        await Set.Include(r => r.Devices)
            .Include(r => r.Events)
            .Include(r => r.Transfers).ThenInclude(t => t.Deliveries)
            .Include(r => r.Transfers).ThenInclude(t => t.Blob)
            .Where(r => r.Status == RoomStatus.Expired)
            .ToListAsync(cancellationToken);
}
