namespace RelayRoom.Core.Domain;

public sealed class Device : IEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DeviceKind Kind { get; set; }
    public DeviceRole Role { get; set; }
    public DeviceConnectionStatus Status { get; set; }
    public string? UserAgent { get; set; }
    public string? ConnectionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public int AvatarSeed { get; set; }

    public Room Room { get; set; } = null!;
    public List<Transfer> SentTransfers { get; set; } = [];
    public List<TransferDelivery> Deliveries { get; set; } = [];
}
