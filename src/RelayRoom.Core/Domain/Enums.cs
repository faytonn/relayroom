namespace RelayRoom.Core.Domain;

public enum RoomStatus
{
    Active,
    Expired,
    Purged
}

public enum DeviceKind
{
    Desktop,
    Mobile,
    Other
}

public enum DeviceRole
{
    Host,
    Guest
}

public enum DeviceConnectionStatus
{
    Connected,
    Disconnected
}

public enum EncryptionMode
{
    None,
    Client
}

public enum TransferKind
{
    Text,
    Link,
    File,
    Image,
    Voice
}

public enum TransferStatus
{
    Uploading,
    Available,
    Expired,
    Failed,
    Deleted
}

public enum DeliveryStatus
{
    Offered,
    Accepted,
    Downloading,
    Completed,
    Declined,
    Failed
}

public enum BlobUploadStatus
{
    Pending,
    Completing,
    Complete,
    Aborted
}

public enum RoomEventType
{
    DeviceJoined,
    DeviceLeft,
    DeviceUpdated,
    TransferCreated,
    TransferProgress,
    TransferAvailable,
    TransferFailed,
    TransferDeleted,
    RoomExpiring,
    RoomExpired,
    RoomClosed
}
