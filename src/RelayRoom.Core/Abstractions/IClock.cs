namespace RelayRoom.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
