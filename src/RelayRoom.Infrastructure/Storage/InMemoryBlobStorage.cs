using System.Collections.Concurrent;
using RelayRoom.Core.Abstractions;

namespace RelayRoom.Infrastructure.Storage;

public sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public bool UsesExternalDownloadUris => false;

    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        _blobs[key] = copy.ToArray();
        return Task.CompletedTask;
    }

    public Task AppendAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var incoming = new MemoryStream();
        content.CopyTo(incoming);
        _blobs.AddOrUpdate(key, incoming.ToArray(), (_, existing) => [.. existing, .. incoming.ToArray()]);
        return Task.CompletedTask;
    }

    public Task<Uri> GetReadUriAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        if (!_blobs.ContainsKey(key))
        {
            throw new FileNotFoundException(key);
        }

        return Task.FromResult(new Uri($"memory://blobs/{Uri.EscapeDataString(key)}"));
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryGetValue(key, out var bytes))
        {
            throw new FileNotFoundException(key);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _blobs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _blobs.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
