namespace RelayRoom.Core.Domain;

public sealed class BlobObject : IEntity
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public BlobUploadStatus UploadStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Transfer? Transfer { get; set; }
}
