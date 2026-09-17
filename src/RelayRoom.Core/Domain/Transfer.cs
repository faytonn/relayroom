namespace RelayRoom.Core.Domain;

public sealed class Transfer : IEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderDeviceId { get; set; }
    public Guid? TargetDeviceId { get; set; }
    public TransferKind Kind { get; set; }
    public TransferStatus Status { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public string? TextBody { get; set; }
    public Guid? BlobId { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }

    public Room Room { get; set; } = null!;
    public Device Sender { get; set; } = null!;
    public Device? Target { get; set; }
    public BlobObject? Blob { get; set; }
    public List<TransferDelivery> Deliveries { get; set; } = [];
}
