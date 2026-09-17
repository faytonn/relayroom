using RelayRoom.Application.Persistence;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;
using RelayRoom.Core.Rules;

namespace RelayRoom.Application.Services;

public sealed class TransferService(
    IUnitOfWork unitOfWork,
    IClock clock,
    IBlobStorage blobs,
    IRoomNotifier notifier) : ITransferService
{
    public async Task<Transfer> CreateInlineAsync(
        Guid roomId,
        Guid senderDeviceId,
        TransferKind kind,
        string? textBody,
        Guid? targetDeviceId,
        CancellationToken cancellationToken)
    {
        if (kind is not (TransferKind.Text or TransferKind.Link))
        {
            throw new ValidationException("Inline transfers must be text or link.");
        }

        var (room, sender) = await LoadActorAsync(roomId, senderDeviceId, cancellationToken);
        TransferRules.EnsureCanCreate(room, clock.UtcNow);
        var body = TransferRules.ValidateTextBody(textBody);
        if (kind is TransferKind.Link)
        {
            body = TransferRules.ValidateHttpUrl(body);
        }

        var now = clock.UtcNow;
        var transfer = new Transfer
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            SenderDeviceId = sender.Id,
            TargetDeviceId = ResolveTarget(room, targetDeviceId),
            Kind = kind,
            Status = TransferStatus.Available,
            MimeType = kind is TransferKind.Link ? "text/uri-list" : "text/plain",
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(body),
            TextBody = body,
            CreatedAt = now,
            CompletedAt = now
        };

        AttachOffer(transfer, now);
        unitOfWork.Transfers.Add(transfer);
        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.TransferAvailable, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.TransferCreated(room.Id, transfer, cancellationToken);
        await notifier.TransferAvailable(room.Id, transfer, cancellationToken);
        if (transfer.TargetDeviceId is Guid target)
        {
            await notifier.TransferOffered(room.Id, target, transfer, cancellationToken);
        }

        return transfer;
    }

    public async Task<Transfer> CreateUploadIntentAsync(
        Guid roomId,
        Guid senderDeviceId,
        TransferKind kind,
        string? fileName,
        string? mimeType,
        long sizeBytes,
        string? sha256,
        Guid? targetDeviceId,
        CancellationToken cancellationToken)
    {
        if (kind is not (TransferKind.File or TransferKind.Image or TransferKind.Voice))
        {
            throw new ValidationException("Upload intents must be file, image, or voice.");
        }

        var (room, sender) = await LoadActorAsync(roomId, senderDeviceId, cancellationToken);
        TransferRules.EnsureCanCreate(room, clock.UtcNow);
        RoomRules.EnsureCanAcceptBytes(room, sizeBytes);

        var now = clock.UtcNow;
        var blob = new BlobObject
        {
            Id = Guid.CreateVersion7(),
            StorageKey = $"{room.Id:N}/{Guid.CreateVersion7():N}",
            ContentType = MimeAllowList.Normalize(mimeType, kind),
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            UploadStatus = BlobUploadStatus.Pending,
            CreatedAt = now,
            ExpiresAt = room.ExpiresAt
        };

        var transfer = new Transfer
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            SenderDeviceId = sender.Id,
            TargetDeviceId = ResolveTarget(room, targetDeviceId),
            Kind = kind,
            Status = TransferStatus.Uploading,
            FileName = TransferRules.ValidateFileName(fileName),
            MimeType = blob.ContentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Blob = blob,
            CreatedAt = now
        };

        AttachOffer(transfer, now);
        unitOfWork.Blobs.Add(blob);
        unitOfWork.Transfers.Add(transfer);
        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.TransferCreated, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TransferCreated(room.Id, transfer, cancellationToken);
        return transfer;
    }

    public async Task CompleteUploadAsync(Guid transferId, Stream content, long uploadedBytes, CancellationToken cancellationToken)
    {
        var transfer = await LoadTransferAsync(transferId, cancellationToken);
        TransferRules.EnsureUploading(transfer);
        var room = transfer.Room;
        RoomRules.EnsureJoinable(room, clock.UtcNow);

        if (uploadedBytes != transfer.SizeBytes)
        {
            TransferRules.FailUpload(transfer, "Uploaded size did not match the declared size.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.TransferFailed(room.Id, transfer.Id, transfer.FailureReason!, cancellationToken);
            throw new ValidationException(transfer.FailureReason!);
        }

        if (transfer.Blob is null)
        {
            throw new ConflictException("Transfer has no blob.");
        }

        RoomRules.EnsureCanAcceptBytes(room, transfer.SizeBytes);
        await blobs.UploadAsync(transfer.Blob.StorageKey, content, transfer.Blob.ContentType, cancellationToken);
        TransferRules.MarkAvailable(transfer, clock.UtcNow);
        room.UsedBytes += transfer.SizeBytes;
        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.TransferAvailable, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.TransferAvailable(room.Id, transfer, cancellationToken);
        await notifier.RoomQuotaUpdated(room.Id, room.UsedBytes, room.MaxBytes, cancellationToken);
        if (transfer.TargetDeviceId is Guid target)
        {
            await notifier.TransferOffered(room.Id, target, transfer, cancellationToken);
        }
    }

    public async Task ReportUploadProgressAsync(Guid transferId, long uploadedBytes, CancellationToken cancellationToken)
    {
        var transfer = await unitOfWork.Transfers.GetByIdUntrackedAsync(transferId, cancellationToken)
                       ?? throw new NotFoundException("Transfer not found.");
        await notifier.TransferProgress(transfer.RoomId, transfer.Id, uploadedBytes, transfer.SizeBytes, cancellationToken);
    }

    public async Task DeleteAsync(Guid roomId, Guid actorDeviceId, Guid transferId, CancellationToken cancellationToken)
    {
        var (room, actor) = await LoadActorAsync(roomId, actorDeviceId, cancellationToken);
        var transfer = room.Transfers.FirstOrDefault(t => t.Id == transferId) ?? throw new NotFoundException("Transfer not found.");
        TransferRules.EnsureSenderOrHostCanDelete(transfer, actor);
        var wasAvailable = transfer.Status == TransferStatus.Available;
        transfer.Status = TransferStatus.Deleted;
        if (transfer.Blob is not null)
        {
            await blobs.DeleteAsync(transfer.Blob.StorageKey, cancellationToken);
            transfer.Blob.UploadStatus = BlobUploadStatus.Aborted;
            if (wasAvailable)
            {
                room.UsedBytes = Math.Max(0, room.UsedBytes - transfer.SizeBytes);
            }
        }

        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.TransferDeleted, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TransferDeleted(room.Id, transfer.Id, cancellationToken);
        await notifier.RoomQuotaUpdated(room.Id, room.UsedBytes, room.MaxBytes, cancellationToken);
    }

    public async Task<DownloadUrlResult> CreateDownloadUrlAsync(
        Guid transferId,
        Guid deviceId,
        string contentFallbackUrl,
        CancellationToken cancellationToken)
    {
        var transfer = await LoadTransferAsync(transferId, cancellationToken);
        EnsureMember(transfer.Room, deviceId);
        RoomRules.EnsureJoinable(transfer.Room, clock.UtcNow);
        TransferRules.EnsureAvailable(transfer);

        if (transfer.Kind is TransferKind.Text or TransferKind.Link)
        {
            return new DownloadUrlResult(contentFallbackUrl, clock.UtcNow.Add(RoomLimits.DownloadUrlLifetime), Inline: true);
        }

        if (transfer.Blob is null)
        {
            throw new NotFoundException("Blob not found.");
        }

        var expiresAt = clock.UtcNow.Add(RoomLimits.DownloadUrlLifetime);
        if (blobs.UsesExternalDownloadUris)
        {
            var uri = await blobs.GetReadUriAsync(transfer.Blob.StorageKey, RoomLimits.DownloadUrlLifetime, cancellationToken);
            return new DownloadUrlResult(uri.ToString(), expiresAt, Inline: false);
        }

        return new DownloadUrlResult(contentFallbackUrl, expiresAt, Inline: false);
    }

    public async Task<(Transfer Transfer, Stream Stream)> OpenContentAsync(Guid transferId, Guid deviceId, CancellationToken cancellationToken)
    {
        var transfer = await LoadTransferAsync(transferId, cancellationToken);
        EnsureMember(transfer.Room, deviceId);
        RoomRules.EnsureJoinable(transfer.Room, clock.UtcNow);
        TransferRules.EnsureAvailable(transfer);

        if (transfer.Kind is TransferKind.Text or TransferKind.Link)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(transfer.TextBody ?? string.Empty);
            return (transfer, new MemoryStream(bytes));
        }

        if (transfer.Blob is null)
        {
            throw new NotFoundException("Blob not found.");
        }

        var stream = await blobs.OpenReadAsync(transfer.Blob.StorageKey, cancellationToken);
        return (transfer, stream);
    }

    public async Task<TransferDelivery> AckAsync(
        Guid transferId,
        Guid deviceId,
        DeliveryStatus status,
        long? progressBytes,
        CancellationToken cancellationToken)
    {
        var transfer = await LoadTransferAsync(transferId, cancellationToken);
        EnsureMember(transfer.Room, deviceId);
        RoomRules.EnsureJoinable(transfer.Room, clock.UtcNow);

        var delivery = transfer.Deliveries.FirstOrDefault(d => d.DeviceId == deviceId);
        if (delivery is null)
        {
            delivery = new TransferDelivery
            {
                Id = Guid.CreateVersion7(),
                TransferId = transfer.Id,
                DeviceId = deviceId,
                Status = DeliveryStatus.Offered,
                UpdatedAt = clock.UtcNow
            };
            transfer.Deliveries.Add(delivery);
        }

        TransferRules.ApplyAck(delivery, status, progressBytes, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TransferDeliveryUpdated(transfer.RoomId, delivery, cancellationToken);
        return delivery;
    }

    public async Task<Transfer> GetOwnedUploadingAsync(Guid transferId, Guid senderDeviceId, CancellationToken cancellationToken)
    {
        var transfer = await LoadTransferAsync(transferId, cancellationToken);
        if (transfer.SenderDeviceId != senderDeviceId)
        {
            throw new ForbiddenException("Only the sender can upload this transfer.");
        }

        TransferRules.EnsureUploading(transfer);
        RoomRules.EnsureJoinable(transfer.Room, clock.UtcNow);
        return transfer;
    }

    private async Task<(Room Room, Device Device)> LoadActorAsync(Guid roomId, Guid deviceId, CancellationToken cancellationToken)
    {
        var room = await unitOfWork.Rooms.GetByIdWithDevicesAndTransfersAsync(roomId, cancellationToken)
                   ?? throw new NotFoundException("Room not found.");
        var device = EnsureMember(room, deviceId);
        return (room, device);
    }

    private async Task<Transfer> LoadTransferAsync(Guid transferId, CancellationToken cancellationToken) =>
        await unitOfWork.Transfers.GetWithRoomAsync(transferId, cancellationToken)
        ?? throw new NotFoundException("Transfer not found.");

    private static Device EnsureMember(Room room, Guid deviceId) =>
        room.Devices.FirstOrDefault(d => d.Id == deviceId)
        ?? throw new ForbiddenException("Device is not a member of this room.");

    private static Guid? ResolveTarget(Room room, Guid? targetDeviceId)
    {
        if (targetDeviceId is null)
        {
            return null;
        }

        if (room.Devices.All(d => d.Id != targetDeviceId))
        {
            throw new ValidationException("Target device is not in this room.");
        }

        return targetDeviceId;
    }

    private static void AttachOffer(Transfer transfer, DateTimeOffset now)
    {
        if (transfer.TargetDeviceId is Guid target)
        {
            transfer.Deliveries.Add(new TransferDelivery
            {
                Id = Guid.CreateVersion7(),
                TransferId = transfer.Id,
                DeviceId = target,
                Status = DeliveryStatus.Offered,
                UpdatedAt = now
            });
        }
    }

    private static RoomEvent NewEvent(Guid roomId, RoomEventType type, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RoomId = roomId,
            Type = type,
            CreatedAt = now
        };
}
