using RelayRoom.Core.Domain;

namespace RelayRoom.Core.Abstractions;

public interface IRoomNotifier
{
    Task DeviceJoined(Guid roomId, Device device, CancellationToken cancellationToken = default);
    Task DeviceLeft(Guid roomId, Device device, CancellationToken cancellationToken = default);
    Task DeviceUpdated(Guid roomId, Device device, CancellationToken cancellationToken = default);
    Task DeviceConnectionChanged(Guid roomId, Device device, CancellationToken cancellationToken = default);
    Task TransferCreated(Guid roomId, Transfer transfer, CancellationToken cancellationToken = default);
    Task TransferProgress(Guid roomId, Guid transferId, long uploadedBytes, long totalBytes, CancellationToken cancellationToken = default);
    Task TransferAvailable(Guid roomId, Transfer transfer, CancellationToken cancellationToken = default);
    Task TransferOffered(Guid roomId, Guid targetDeviceId, Transfer transfer, CancellationToken cancellationToken = default);
    Task TransferFailed(Guid roomId, Guid transferId, string reason, CancellationToken cancellationToken = default);
    Task TransferDeleted(Guid roomId, Guid transferId, CancellationToken cancellationToken = default);
    Task TransferDeliveryUpdated(Guid roomId, TransferDelivery delivery, CancellationToken cancellationToken = default);
    Task RoomQuotaUpdated(Guid roomId, long usedBytes, long maxBytes, CancellationToken cancellationToken = default);
    Task RoomExpiring(Guid roomId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task RoomExpired(Guid roomId, CancellationToken cancellationToken = default);
}
