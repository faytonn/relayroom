namespace RelayRoom.Core.Rules;

public static class RoomLimits
{
    public const int MaxDevices = 5;
    public const int PublicCodeLength = 6;
    public const long MaxFileBytes = 100L * 1024 * 1024;
    public const long MaxRoomBytes = 500L * 1024 * 1024;
    public const int MaxTextBytes = 20 * 1024;
    public const int MaxDisplayNameLength = 40;
    public const int MaxFileNameLength = 255;
    public static readonly TimeSpan RoomTtl = TimeSpan.FromHours(1);
    public static readonly TimeSpan ExpiringWarning = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DownloadUrlLifetime = TimeSpan.FromMinutes(2);
    public const int JoinAttemptsPerMinute = 5;
}
