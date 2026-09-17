using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;

namespace RelayRoom.Core.Rules;

public static class RoomRules
{
    public static bool IsJoinable(Room room, DateTimeOffset now) =>
        room.Status == RoomStatus.Active && room.ExpiresAt > now;

    public static void EnsureJoinable(Room room, DateTimeOffset now)
    {
        if (!IsJoinable(room, now))
        {
            throw new RoomExpiredException();
        }
    }

    public static void EnsureHasCapacity(Room room)
    {
        if (room.Devices.Count >= RoomLimits.MaxDevices)
        {
            throw new RoomFullException();
        }
    }

    public static void EnsureCanAcceptBytes(Room room, long additionalBytes)
    {
        if (additionalBytes < 0)
        {
            throw new ValidationException("Size cannot be negative.");
        }

        if (additionalBytes > RoomLimits.MaxFileBytes)
        {
            throw new QuotaExceededException($"File exceeds the {RoomLimits.MaxFileBytes} byte limit.");
        }

        if (room.UsedBytes + additionalBytes > room.MaxBytes)
        {
            throw new QuotaExceededException("Room storage quota would be exceeded.");
        }
    }

    public static bool ShouldWarnExpiring(Room room, DateTimeOffset now) =>
        room.Status == RoomStatus.Active
        && !room.ExpiringWarningSent
        && room.ExpiresAt - RoomLimits.ExpiringWarning <= now
        && room.ExpiresAt > now;

    public static bool IsExpired(Room room, DateTimeOffset now) =>
        room.Status == RoomStatus.Active && room.ExpiresAt <= now;

    public static void EnsureHost(Device device)
    {
        if (device.Role != DeviceRole.Host)
        {
            throw new ForbiddenException("Only the host can close this room.");
        }
    }

    public static string ValidateDisplayName(string? displayName, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
        if (name.Length > RoomLimits.MaxDisplayNameLength)
        {
            name = name[..RoomLimits.MaxDisplayNameLength];
        }

        if (name.Length == 0)
        {
            throw new ValidationException("Display name cannot be empty.");
        }

        return name;
    }
}
