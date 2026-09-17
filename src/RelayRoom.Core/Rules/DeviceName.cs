using RelayRoom.Core.Domain;

namespace RelayRoom.Core.Rules;

public static class DeviceName
{
    public static (DeviceKind Kind, string DisplayName) FromUserAgent(string? userAgent)
    {
        var ua = userAgent ?? string.Empty;
        var mobile = ua.Contains("Mobi", StringComparison.OrdinalIgnoreCase)
                     || ua.Contains("Android", StringComparison.OrdinalIgnoreCase)
                     || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
                     || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase);

        var browser =
            ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" :
            ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
            ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
            ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari" :
            "Browser";

        var os =
            ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
            ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
            ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone" :
            ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad" :
            ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ? "macOS" :
            ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" :
            "device";

        var kind = mobile ? DeviceKind.Mobile : ua.Length == 0 ? DeviceKind.Other : DeviceKind.Desktop;
        return (kind, $"{browser} on {os}");
    }
}
