using RelayRoom.Application.Persistence;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;
using RelayRoom.Core.Rules;

namespace RelayRoom.Application.Services;

public sealed class RoomSessionService(
    IUnitOfWork unitOfWork,
    IClock clock,
    ITokenService tokens,
    IRoomNotifier notifier) : IRoomSessionService
{
    public async Task<RoomSessionResult> CreateAsync(string? displayName, string? userAgent, string publicBaseUrl, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (kind, fallbackName) = DeviceName.FromUserAgent(userAgent);
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            PublicCode = await AllocateCodeAsync(cancellationToken),
            Status = RoomStatus.Active,
            CreatedAt = now,
            ExpiresAt = now.Add(RoomLimits.RoomTtl),
            UsedBytes = 0,
            MaxBytes = RoomLimits.MaxRoomBytes,
            EncryptionMode = EncryptionMode.None
        };

        var host = NewDevice(room.Id, DeviceRole.Host, kind, RoomRules.ValidateDisplayName(displayName, fallbackName), userAgent, now);
        room.CreatorDeviceId = host.Id;
        room.Devices.Add(host);
        room.Events.Add(NewEvent(room.Id, RoomEventType.DeviceJoined, now));
        unitOfWork.Rooms.Add(room);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokens.Issue(host, room);
        return ToSession(room, host, token, publicBaseUrl);
    }

    public async Task<RoomSessionResult> JoinAsync(string code, string? displayName, string? userAgent, string publicBaseUrl, CancellationToken cancellationToken)
    {
        var normalized = RoomCodeGenerator.Normalize(code);
        if (!RoomCodeGenerator.IsValid(normalized))
        {
            throw new ValidationException("Join code is invalid.");
        }

        var now = clock.UtcNow;
        var room = await unitOfWork.Rooms.GetActiveByPublicCodeAsync(normalized, cancellationToken)
                   ?? throw new NotFoundException("Room not found.");

        RoomRules.EnsureJoinable(room, now);
        RoomRules.EnsureHasCapacity(room);

        var (kind, fallbackName) = DeviceName.FromUserAgent(userAgent);
        var guest = NewDevice(room.Id, DeviceRole.Guest, kind, RoomRules.ValidateDisplayName(displayName, fallbackName), userAgent, now);
        unitOfWork.Devices.Add(guest);
        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.DeviceJoined, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.DeviceJoined(room.Id, guest, cancellationToken);
        var token = tokens.Issue(guest, room);
        return ToSession(room, guest, token, publicBaseUrl);
    }

    public async Task<RoomDetail> GetAsync(Guid roomId, Guid deviceId, CancellationToken cancellationToken)
    {
        var room = await LoadRoomGraph(roomId, cancellationToken);
        EnsureMember(room, deviceId);
        RoomRules.EnsureJoinable(room, clock.UtcNow);
        return RoomDetail.From(room);
    }

    public async Task CloseAsync(Guid roomId, Guid deviceId, CancellationToken cancellationToken)
    {
        var room = await LoadRoomGraph(roomId, cancellationToken);
        var actor = EnsureMember(room, deviceId);
        RoomRules.EnsureHost(actor);
        await ExpireRoomAsync(room, RoomEventType.RoomClosed, cancellationToken);
    }

    public async Task<Device> RenameAsync(Guid roomId, Guid deviceId, string displayName, CancellationToken cancellationToken)
    {
        var room = await LoadRoomGraph(roomId, cancellationToken);
        var device = EnsureMember(room, deviceId);
        RoomRules.EnsureJoinable(room, clock.UtcNow);
        device.DisplayName = RoomRules.ValidateDisplayName(displayName, device.DisplayName);
        device.LastSeenAt = clock.UtcNow;
        unitOfWork.RoomEvents.Add(NewEvent(room.Id, RoomEventType.DeviceUpdated, device.LastSeenAt));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.DeviceUpdated(room.Id, device, cancellationToken);
        return device;
    }

    public async Task MarkConnectedAsync(Guid roomId, Guid deviceId, string connectionId, CancellationToken cancellationToken)
    {
        var room = await LoadRoomGraph(roomId, cancellationToken);
        var device = EnsureMember(room, deviceId);
        var wasDisconnected = device.Status == DeviceConnectionStatus.Disconnected;
        device.Status = DeviceConnectionStatus.Connected;
        device.ConnectionId = connectionId;
        device.LastSeenAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (wasDisconnected)
        {
            await notifier.DeviceConnectionChanged(room.Id, device, cancellationToken);
        }
    }

    public async Task MarkDisconnectedAsync(Guid roomId, Guid deviceId, string connectionId, CancellationToken cancellationToken)
    {
        var matches = await unitOfWork.Devices.GetWhereAsync(d => d.Id == deviceId && d.RoomId == roomId, cancellationToken);
        var device = matches.FirstOrDefault();
        if (device is null)
        {
            return;
        }

        if (device.ConnectionId is not null && device.ConnectionId != connectionId)
        {
            return;
        }

        device.Status = DeviceConnectionStatus.Disconnected;
        device.ConnectionId = null;
        device.LastSeenAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.DeviceLeft(roomId, device, cancellationToken);
    }

    public async Task ExpireRoomAsync(Room room, RoomEventType eventType, CancellationToken cancellationToken)
    {
        if (room.Status != RoomStatus.Active)
        {
            return;
        }

        room.Status = RoomStatus.Expired;
        foreach (var transfer in room.Transfers.Where(t => t.Status is TransferStatus.Uploading or TransferStatus.Available))
        {
            transfer.Status = TransferStatus.Expired;
            if (transfer.Blob is { UploadStatus: BlobUploadStatus.Pending or BlobUploadStatus.Completing })
            {
                transfer.Blob.UploadStatus = BlobUploadStatus.Aborted;
            }
        }

        unitOfWork.RoomEvents.Add(NewEvent(room.Id, eventType, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RoomExpired(room.Id, cancellationToken);
    }

    private async Task<string> AllocateCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var code = RoomCodeGenerator.Create();
            if (!await unitOfWork.Rooms.ActiveCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new ConflictException("Could not allocate a unique room code.");
    }

    private async Task<Room> LoadRoomGraph(Guid roomId, CancellationToken cancellationToken) =>
        await unitOfWork.Rooms.GetGraphByIdAsync(roomId, cancellationToken)
        ?? throw new NotFoundException("Room not found.");

    private static Device EnsureMember(Room room, Guid deviceId)
    {
        var device = room.Devices.FirstOrDefault(d => d.Id == deviceId);
        return device ?? throw new ForbiddenException("Device is not a member of this room.");
    }

    private static Device NewDevice(Guid roomId, DeviceRole role, DeviceKind kind, string displayName, string? userAgent, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RoomId = roomId,
            DisplayName = displayName,
            Kind = kind,
            Role = role,
            Status = DeviceConnectionStatus.Disconnected,
            UserAgent = userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 512)],
            CreatedAt = now,
            LastSeenAt = now,
            AvatarSeed = Random.Shared.Next(1, 10_000)
        };

    private static RoomEvent NewEvent(Guid roomId, RoomEventType type, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RoomId = roomId,
            Type = type,
            CreatedAt = now
        };

    private static RoomSessionResult ToSession(Room room, Device device, DeviceToken token, string publicBaseUrl)
    {
        var joinUrl = $"{publicBaseUrl.TrimEnd('/')}/r/{room.PublicCode}";
        return new RoomSessionResult(room, device, token, joinUrl);
    }
}
