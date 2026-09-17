using System.Text;
using Microsoft.Extensions.Options;
using RelayRoom.Application.Services;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace RelayRoom.Api.Uploads;

public sealed class TusDiskOptions
{
    public string Path { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "relayroom-tus");
}

public static class TusConfiguration
{
    public static DefaultTusConfiguration Create(IServiceProvider services, HttpContext http)
    {
        var options = services.GetRequiredService<IOptions<TusDiskOptions>>().Value;
        Directory.CreateDirectory(options.Path);
        var store = new TusDiskStore(options.Path);

        return new DefaultTusConfiguration
        {
            Store = store,
            MaxAllowedUploadSizeInBytes = (int)Core.Rules.RoomLimits.MaxFileBytes,
            MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
            Events = new Events
            {
                OnAuthorizeAsync = async context =>
                {
                    if (http.User.Identity?.IsAuthenticated != true)
                    {
                        context.FailRequest(System.Net.HttpStatusCode.Unauthorized);
                    }

                    await Task.CompletedTask;
                },
                OnBeforeWriteAsync = async context =>
                {
                    var file = await store.GetFileAsync(context.FileId, context.CancellationToken);
                    if (file is null)
                    {
                        return;
                    }

                    var metadata = await file.GetMetadataAsync(context.CancellationToken);
                    var transferId = ReadTransferId(metadata);
                    if (transferId is null)
                    {
                        return;
                    }

                    var offset = await store.GetUploadOffsetAsync(context.FileId, context.CancellationToken);
                    using var scope = http.RequestServices.CreateScope();
                    var transfers = scope.ServiceProvider.GetRequiredService<ITransferService>();
                    await transfers.ReportUploadProgressAsync(transferId.Value, offset, context.CancellationToken);
                },
                OnBeforeCreateAsync = async context =>
                {
                    try
                    {
                        var transferId = ReadTransferId(context.Metadata);
                        if (transferId is null)
                        {
                            context.FailRequest("transferId metadata is required.");
                            return;
                        }

                        using var scope = http.RequestServices.CreateScope();
                        var transfers = scope.ServiceProvider.GetRequiredService<ITransferService>();
                        var deviceId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                       ?? http.User.FindFirst("sub")?.Value;
                        if (deviceId is null || !Guid.TryParse(deviceId, out var senderId))
                        {
                            context.FailRequest(System.Net.HttpStatusCode.Unauthorized);
                            return;
                        }

                        var transfer = await transfers.GetOwnedUploadingAsync(transferId.Value, senderId, context.CancellationToken);
                        if (context.UploadLength != transfer.SizeBytes)
                        {
                            context.FailRequest("Upload-Length must match the declared transfer size.");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.FailRequest(ex.Message);
                    }
                },
                OnFileCompleteAsync = async context =>
                {
                    var file = await context.GetFileAsync();
                    var metadata = await file.GetMetadataAsync(context.CancellationToken);
                    var transferId = ReadTransferId(metadata);
                    if (transferId is null)
                    {
                        return;
                    }

                    var length = await store.GetUploadOffsetAsync(context.FileId, context.CancellationToken);
                    await using (var content = await file.GetContentAsync(context.CancellationToken))
                    {
                        using var scope = http.RequestServices.CreateScope();
                        var transfers = scope.ServiceProvider.GetRequiredService<ITransferService>();
                        await transfers.CompleteUploadAsync(transferId.Value, content, length, context.CancellationToken);
                    }

                    await store.DeleteFileAsync(context.FileId, context.CancellationToken);
                }
            }
        };
    }

    private static Guid? ReadTransferId(Dictionary<string, Metadata> metadata)
    {
        if (!metadata.TryGetValue("transferId", out var value))
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(value.GetBytes());
        return Guid.TryParse(text, out var id) ? id : null;
    }
}
