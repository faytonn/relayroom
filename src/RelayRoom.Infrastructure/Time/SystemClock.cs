using RelayRoom.Core.Abstractions;

namespace RelayRoom.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
