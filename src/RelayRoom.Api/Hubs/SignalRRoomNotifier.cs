using Microsoft.AspNetCore.SignalR;
using RelayRoom.Application.Contracts;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;

namespace RelayRoom.Api.Hubs;

public sealed class SignalRRoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public Task DeviceJoined(Guid roomId, Device device, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("DeviceJoined", DeviceResponse.From(device), cancellationToken);

    public Task DeviceLeft(Guid roomId, Device device, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("DeviceLeft", DeviceResponse.From(device), cancellationToken);

    public Task DeviceUpdated(Guid roomId, Device device, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("DeviceUpdated", DeviceResponse.From(device), cancellationToken);

    public Task DeviceConnectionChanged(Guid roomId, Device device, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("DeviceConnectionChanged", DeviceResponse.From(device), cancellationToken);

    public Task TransferCreated(Guid roomId, Transfer transfer, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferCreated", TransferResponse.From(transfer), cancellationToken);

    public Task TransferProgress(Guid roomId, Guid transferId, long uploadedBytes, long totalBytes, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferProgress", new { transferId, uploadedBytes, totalBytes }, cancellationToken);

    public Task TransferAvailable(Guid roomId, Transfer transfer, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferAvailable", TransferResponse.From(transfer), cancellationToken);

    public Task TransferOffered(Guid roomId, Guid targetDeviceId, Transfer transfer, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(DeviceGroup(targetDeviceId)).SendAsync("TransferOffered", TransferResponse.From(transfer), cancellationToken);

    public Task TransferFailed(Guid roomId, Guid transferId, string reason, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferFailed", new { transferId, reason }, cancellationToken);

    public Task TransferDeleted(Guid roomId, Guid transferId, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferDeleted", new { transferId }, cancellationToken);

    public Task TransferDeliveryUpdated(Guid roomId, TransferDelivery delivery, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("TransferDeliveryUpdated", TransferDeliveryResponse.From(delivery), cancellationToken);

    public Task RoomQuotaUpdated(Guid roomId, long usedBytes, long maxBytes, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("RoomQuotaUpdated", new { usedBytes, maxBytes }, cancellationToken);

    public Task RoomExpiring(Guid roomId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("RoomExpiring", new { expiresAt, secondsLeft = Math.Max(0, (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds) }, cancellationToken);

    public Task RoomExpired(Guid roomId, CancellationToken cancellationToken = default) =>
        Group(roomId).SendAsync("RoomExpired", new { roomId }, cancellationToken);

    private IClientProxy Group(Guid roomId) => hub.Clients.Group(RoomGroup(roomId));

    public static string RoomGroup(Guid roomId) => $"room:{roomId:N}";
    public static string DeviceGroup(Guid deviceId) => $"device:{deviceId:N}";
}
