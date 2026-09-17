using RelayRoom.Core.Abstractions;

namespace RelayRoom.Infrastructure.Notifications;

public sealed class NullRoomNotifier : IRoomNotifier
{
    public static NullRoomNotifier Instance { get; } = new();

    public Task DeviceJoined(Guid roomId, Core.Domain.Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeviceLeft(Guid roomId, Core.Domain.Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeviceUpdated(Guid roomId, Core.Domain.Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeviceConnectionChanged(Guid roomId, Core.Domain.Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferCreated(Guid roomId, Core.Domain.Transfer transfer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferProgress(Guid roomId, Guid transferId, long uploadedBytes, long totalBytes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferAvailable(Guid roomId, Core.Domain.Transfer transfer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferOffered(Guid roomId, Guid targetDeviceId, Core.Domain.Transfer transfer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferFailed(Guid roomId, Guid transferId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferDeleted(Guid roomId, Guid transferId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TransferDeliveryUpdated(Guid roomId, Core.Domain.TransferDelivery delivery, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RoomQuotaUpdated(Guid roomId, long usedBytes, long maxBytes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RoomExpiring(Guid roomId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RoomExpired(Guid roomId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
