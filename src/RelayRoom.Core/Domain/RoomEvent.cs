namespace RelayRoom.Core.Domain;

public sealed class RoomEvent : IEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public RoomEventType Type { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Room Room { get; set; } = null!;
}
