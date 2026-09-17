using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Services;

public interface IRoomSessionService
{
    Task<RoomSessionResult> CreateAsync(string? displayName, string? userAgent, string publicBaseUrl, CancellationToken cancellationToken);
    Task<RoomSessionResult> JoinAsync(string code, string? displayName, string? userAgent, string publicBaseUrl, CancellationToken cancellationToken);
    Task<RoomDetail> GetAsync(Guid roomId, Guid deviceId, CancellationToken cancellationToken);
    Task CloseAsync(Guid roomId, Guid deviceId, CancellationToken cancellationToken);
    Task<Device> RenameAsync(Guid roomId, Guid deviceId, string displayName, CancellationToken cancellationToken);
    Task MarkConnectedAsync(Guid roomId, Guid deviceId, string connectionId, CancellationToken cancellationToken);
    Task MarkDisconnectedAsync(Guid roomId, Guid deviceId, string connectionId, CancellationToken cancellationToken);
    Task ExpireRoomAsync(Room room, RoomEventType eventType, CancellationToken cancellationToken);
}

public sealed record RoomSessionResult(Room Room, Device Device, DeviceToken Token, string JoinUrl);

public sealed record RoomDetail(Room Room, IReadOnlyList<Device> Devices, IReadOnlyList<Transfer> Transfers)
{
    public static RoomDetail From(Room room) => new(room, room.Devices, room.Transfers);
}
