namespace RelayRoom.Core.Abstractions;

public interface IBlobStorage
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task AppendAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<Uri> GetReadUriAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    bool UsesExternalDownloadUris { get; }
}
