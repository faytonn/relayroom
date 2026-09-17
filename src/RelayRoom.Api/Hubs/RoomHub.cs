using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RelayRoom.Api.Auth;
using RelayRoom.Application.Services;

namespace RelayRoom.Api.Hubs;

[Authorize]
public sealed class RoomHub(IRoomSessionService rooms) : Hub
{
    public const string Path = "/hubs/room";

    public override async Task OnConnectedAsync()
    {
        var roomId = Context.User!.GetRoomId();
        var deviceId = Context.User!.GetDeviceId();
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRRoomNotifier.RoomGroup(roomId));
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRRoomNotifier.DeviceGroup(deviceId));
        await rooms.MarkConnectedAsync(roomId, deviceId, Context.ConnectionId, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User is not null)
        {
            try
            {
                var roomId = Context.User.GetRoomId();
                var deviceId = Context.User.GetDeviceId();
                await rooms.MarkDisconnectedAsync(roomId, deviceId, Context.ConnectionId, CancellationToken.None);
            }
            catch
            {
                // Token may already be invalid after expiry; ignore disconnect bookkeeping failures.
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task ReportDownloadProgress(Guid transferId, long bytes) =>
        Clients.OthersInGroup(SignalRRoomNotifier.RoomGroup(Context.User!.GetRoomId()))
            .SendAsync("TransferDeliveryUpdated", new { transferId, deviceId = Context.User!.GetDeviceId(), progressBytes = bytes, status = "Downloading" });
}
