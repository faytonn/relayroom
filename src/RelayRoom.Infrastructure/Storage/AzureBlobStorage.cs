using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using RelayRoom.Core.Abstractions;

namespace RelayRoom.Infrastructure.Storage;

public sealed class AzureBlobStorage(BlobServiceClient blobServiceClient) : IBlobStorage
{
    public const string ContainerName = "relayroom";
    private readonly BlobContainerClient _container = blobServiceClient.GetBlobContainerClient(ContainerName);

    public bool UsesExternalDownloadUris { get; } = blobServiceClient.CanGenerateAccountSasUri;

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
    }

    public async Task AppendAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient(key);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, cancellationToken);
            return;
        }

        await using var existing = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        await using var combined = new MemoryStream();
        await existing.CopyToAsync(combined, cancellationToken);
        await content.CopyToAsync(combined, cancellationToken);
        combined.Position = 0;
        await blob.UploadAsync(combined, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
    }

    public async Task<Uri> GetReadUriAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient(key);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new FileNotFoundException(key);
        }

        if (blob.CanGenerateSasUri)
        {
            return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(lifetime));
        }

        throw new InvalidOperationException("Blob client cannot generate SAS URIs. Stream content through the API instead.");
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(key);
        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _container.DeleteBlobIfExistsAsync(key, cancellationToken: cancellationToken);
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await foreach (var blob in _container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            await _container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
        }
    }
}
