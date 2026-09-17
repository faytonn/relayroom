using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;

namespace RelayRoom.Core.Rules;

public static class MimeAllowList
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/heic",
        "image/heif",
        "audio/webm",
        "audio/mp4",
        "audio/mpeg",
        "audio/ogg",
        "audio/wav",
        "audio/x-wav",
        "application/pdf",
        "application/zip",
        "application/json",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/octet-stream"
    };

    public static string Normalize(string? mimeType, TransferKind kind)
    {
        var mime = string.IsNullOrWhiteSpace(mimeType)
            ? kind switch
            {
                TransferKind.Image => "application/octet-stream",
                TransferKind.Voice => "audio/webm",
                TransferKind.Text => "text/plain",
                TransferKind.Link => "text/uri-list",
                _ => "application/octet-stream"
            }
            : mimeType.Split(';', 2)[0].Trim();

        if (kind is TransferKind.File or TransferKind.Image or TransferKind.Voice
            && !Allowed.Contains(mime))
        {
            throw new ValidationException($"MIME type '{mime}' is not allowed.");
        }

        if (kind is TransferKind.Image && !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Image transfers must use an image MIME type.");
        }

        if (kind is TransferKind.Voice && !mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Voice transfers must use an audio MIME type.");
        }

        return mime;
    }
}
