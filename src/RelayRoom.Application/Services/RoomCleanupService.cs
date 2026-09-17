using RelayRoom.Application.Persistence;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Rules;

namespace RelayRoom.Application.Services;

public sealed class RoomCleanupService(
    IUnitOfWork unitOfWork,
    IClock clock,
    IBlobStorage blobs,
    IRoomNotifier notifier) : IRoomCleanupService
{
    public async Task<CleanupResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var warned = 0;
        var expired = 0;
        var purged = 0;

        var expiring = await unitOfWork.Rooms.ListExpiringAsync(now, RoomLimits.ExpiringWarning, cancellationToken);
        foreach (var room in expiring)
        {
            room.ExpiringWarningSent = true;
            unitOfWork.RoomEvents.Add(new RoomEvent
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                Type = RoomEventType.RoomExpiring,
                CreatedAt = now
            });
            warned++;
            await notifier.RoomExpiring(room.Id, room.ExpiresAt, cancellationToken);
        }

        var due = await unitOfWork.Rooms.ListDueToExpireAsync(now, cancellationToken);
        foreach (var room in due)
        {
            room.Status = RoomStatus.Expired;
            foreach (var transfer in room.Transfers.Where(t => t.Status is TransferStatus.Uploading or TransferStatus.Available))
            {
                transfer.Status = TransferStatus.Expired;
                if (transfer.Blob is { UploadStatus: BlobUploadStatus.Pending or BlobUploadStatus.Completing })
                {
                    transfer.Blob.UploadStatus = BlobUploadStatus.Aborted;
                }
            }

            unitOfWork.RoomEvents.Add(new RoomEvent
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                Type = RoomEventType.RoomExpired,
                CreatedAt = now
            });
            expired++;
            await notifier.RoomExpired(room.Id, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var toPurge = await unitOfWork.Rooms.ListExpiredForPurgeAsync(cancellationToken);
        foreach (var room in toPurge)
        {
            await blobs.DeletePrefixAsync($"{room.Id:N}/", cancellationToken);
            var blobRows = room.Transfers.Select(t => t.Blob).OfType<BlobObject>().ToList();
            unitOfWork.Deliveries.RemoveRange(room.Transfers.SelectMany(t => t.Deliveries));
            unitOfWork.Transfers.RemoveRange(room.Transfers);
            unitOfWork.Blobs.RemoveRange(blobRows);
            unitOfWork.Devices.RemoveRange(room.Devices);
            unitOfWork.RoomEvents.RemoveRange(room.Events);
            unitOfWork.Rooms.Remove(room);
            purged++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CleanupResult(warned, expired, purged);
    }

    public async Task<int> WarnAndExpireAsync(CancellationToken cancellationToken)
    {
        var result = await RunOnceAsync(cancellationToken);
        return result.Warned + result.Expired;
    }
}
