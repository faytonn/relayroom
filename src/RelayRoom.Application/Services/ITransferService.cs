using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Services;

public interface ITransferService
{
    Task<Transfer> CreateInlineAsync(Guid roomId, Guid senderDeviceId, TransferKind kind, string? textBody, Guid? targetDeviceId, CancellationToken cancellationToken);
    Task<Transfer> CreateUploadIntentAsync(Guid roomId, Guid senderDeviceId, TransferKind kind, string? fileName, string? mimeType, long sizeBytes, string? sha256, Guid? targetDeviceId, CancellationToken cancellationToken);
    Task CompleteUploadAsync(Guid transferId, Stream content, long uploadedBytes, CancellationToken cancellationToken);
    Task ReportUploadProgressAsync(Guid transferId, long uploadedBytes, CancellationToken cancellationToken);
    Task DeleteAsync(Guid roomId, Guid actorDeviceId, Guid transferId, CancellationToken cancellationToken);
    Task<DownloadUrlResult> CreateDownloadUrlAsync(Guid transferId, Guid deviceId, string contentFallbackUrl, CancellationToken cancellationToken);
    Task<(Transfer Transfer, Stream Stream)> OpenContentAsync(Guid transferId, Guid deviceId, CancellationToken cancellationToken);
    Task<TransferDelivery> AckAsync(Guid transferId, Guid deviceId, DeliveryStatus status, long? progressBytes, CancellationToken cancellationToken);
    Task<Transfer> GetOwnedUploadingAsync(Guid transferId, Guid senderDeviceId, CancellationToken cancellationToken);
}

public sealed record DownloadUrlResult(string Url, DateTimeOffset ExpiresAt, bool Inline);
