using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Contracts;

public sealed record CreateRoomRequest(string? DisplayName);

public sealed record JoinRoomRequest(string Code, string? DisplayName);

public sealed record RenameDeviceRequest(string DisplayName);

public sealed record CreateTransferRequest(
    TransferKind Kind,
    string? TextBody,
    string? FileName,
    string? MimeType,
    long? SizeBytes,
    string? Sha256,
    Guid? TargetDeviceId);

public sealed record AckDeliveryRequest(DeliveryStatus Status, long? ProgressBytes);

public sealed record DeviceResponse(
    Guid Id,
    string DisplayName,
    DeviceKind Kind,
    DeviceRole Role,
    DeviceConnectionStatus Status,
    int AvatarSeed,
    DateTimeOffset LastSeenAt)
{
    public static DeviceResponse From(Device device) => new(
        device.Id,
        device.DisplayName,
        device.Kind,
        device.Role,
        device.Status,
        device.AvatarSeed,
        device.LastSeenAt);
}

public sealed record TransferDeliveryResponse(
    Guid Id,
    Guid DeviceId,
    DeliveryStatus Status,
    long ProgressBytes,
    DateTimeOffset UpdatedAt)
{
    public static TransferDeliveryResponse From(TransferDelivery delivery) => new(
        delivery.Id,
        delivery.DeviceId,
        delivery.Status,
        delivery.ProgressBytes,
        delivery.UpdatedAt);
}

public sealed record TransferResponse(
    Guid Id,
    Guid SenderDeviceId,
    Guid? TargetDeviceId,
    TransferKind Kind,
    TransferStatus Status,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? TextBody,
    string? Sha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    IReadOnlyList<TransferDeliveryResponse> Deliveries)
{
    public static TransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.SenderDeviceId,
        transfer.TargetDeviceId,
        transfer.Kind,
        transfer.Status,
        transfer.FileName,
        transfer.MimeType,
        transfer.SizeBytes,
        transfer.Kind is TransferKind.Text or TransferKind.Link ? transfer.TextBody : null,
        transfer.Sha256,
        transfer.CreatedAt,
        transfer.CompletedAt,
        transfer.FailureReason,
        transfer.Deliveries.Select(TransferDeliveryResponse.From).ToList());
}

public sealed record RoomResponse(
    Guid Id,
    string PublicCode,
    string JoinUrl,
    string QrPayload,
    RoomStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    long UsedBytes,
    long MaxBytes,
    EncryptionMode EncryptionMode)
{
    public static RoomResponse From(Room room, string joinUrl) => new(
        room.Id,
        room.PublicCode,
        joinUrl,
        joinUrl,
        room.Status,
        room.CreatedAt,
        room.ExpiresAt,
        room.UsedBytes,
        room.MaxBytes,
        room.EncryptionMode);
}

public sealed record RoomSessionResponse(
    RoomResponse Room,
    DeviceResponse Device,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record RoomDetailResponse(
    RoomResponse Room,
    IReadOnlyList<DeviceResponse> Devices,
    IReadOnlyList<TransferResponse> Transfers);

public sealed record DownloadUrlResponse(string Url, DateTimeOffset ExpiresAt, bool Inline);
