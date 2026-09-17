using System.Security.Claims;
using RelayRoom.Core.Auth;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;

namespace RelayRoom.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetDeviceId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (value is null || !Guid.TryParse(value, out var id))
        {
            throw new ForbiddenException("Device identity is missing.");
        }

        return id;
    }

    public static Guid GetRoomId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(DeviceClaims.RoomId);
        if (value is null || !Guid.TryParse(value, out var id))
        {
            throw new ForbiddenException("Room identity is missing.");
        }

        return id;
    }

    public static DeviceRole GetRole(this ClaimsPrincipal user) =>
        Enum.TryParse<DeviceRole>(user.FindFirstValue(DeviceClaims.Role), out var role) ? role : DeviceRole.Guest;

    public static void EnsureRoom(this ClaimsPrincipal user, Guid roomId)
    {
        if (user.GetRoomId() != roomId)
        {
            throw new ForbiddenException("This token does not belong to that room.");
        }
    }
}
