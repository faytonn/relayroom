using RelayRoom.Core.Domain;

namespace RelayRoom.Core.Abstractions;

public sealed record DeviceToken(string AccessToken, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    DeviceToken Issue(Device device, Room room);
}
