namespace RelayRoom.Core.Domain;

public sealed class Room : IEntity
{
    public Guid Id { get; set; }
    public string PublicCode { get; set; } = string.Empty;
    public RoomStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long UsedBytes { get; set; }
    public long MaxBytes { get; set; }
    public EncryptionMode EncryptionMode { get; set; }
    public Guid? CreatorDeviceId { get; set; }
    public bool ExpiringWarningSent { get; set; }

    public List<Device> Devices { get; set; } = [];
    public List<Transfer> Transfers { get; set; } = [];
    public List<RoomEvent> Events { get; set; } = [];
}
