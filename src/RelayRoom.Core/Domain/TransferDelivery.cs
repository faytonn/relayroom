namespace RelayRoom.Core.Domain;

public sealed class TransferDelivery : IEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public Guid DeviceId { get; set; }
    public DeliveryStatus Status { get; set; }
    public long ProgressBytes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Transfer Transfer { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
