using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;

namespace RelayRoom.Core.Rules;

public static class TransferRules
{
    public static void EnsureCanCreate(Room room, DateTimeOffset now)
    {
        RoomRules.EnsureJoinable(room, now);
    }

    public static void EnsureSenderOrHostCanDelete(Transfer transfer, Device actor)
    {
        if (actor.Id != transfer.SenderDeviceId && actor.Role != DeviceRole.Host)
        {
            throw new ForbiddenException("Only the sender or host can delete this transfer.");
        }
    }

    public static void EnsureAvailable(Transfer transfer)
    {
        if (transfer.Status != TransferStatus.Available)
        {
            throw new ConflictException("Transfer is not available for download.");
        }
    }

    public static void EnsureUploading(Transfer transfer)
    {
        if (transfer.Status != TransferStatus.Uploading)
        {
            throw new ConflictException("Transfer is not accepting uploads.");
        }
    }

    public static TransferStatus FailUpload(Transfer transfer, string reason)
    {
        transfer.Status = TransferStatus.Failed;
        transfer.FailureReason = reason;
        if (transfer.Blob is not null)
        {
            transfer.Blob.UploadStatus = BlobUploadStatus.Aborted;
        }

        return transfer.Status;
    }

    public static TransferStatus MarkAvailable(Transfer transfer, DateTimeOffset now)
    {
        transfer.Status = TransferStatus.Available;
        transfer.CompletedAt = now;
        if (transfer.Blob is not null)
        {
            transfer.Blob.UploadStatus = BlobUploadStatus.Complete;
        }

        return transfer.Status;
    }

    public static DeliveryStatus ApplyAck(TransferDelivery delivery, DeliveryStatus status, long? progressBytes, DateTimeOffset now)
    {
        if (status is DeliveryStatus.Offered)
        {
            throw new ValidationException("Cannot ack a delivery back to Offered.");
        }

        delivery.Status = status;
        if (progressBytes.HasValue)
        {
            delivery.ProgressBytes = Math.Max(0, progressBytes.Value);
        }

        delivery.UpdatedAt = now;
        return delivery.Status;
    }

    public static string ValidateTextBody(string? textBody)
    {
        if (string.IsNullOrWhiteSpace(textBody))
        {
            throw new ValidationException("Text body is required.");
        }

        var body = textBody.Trim();
        if (System.Text.Encoding.UTF8.GetByteCount(body) > RoomLimits.MaxTextBytes)
        {
            throw new ValidationException($"Text exceeds the {RoomLimits.MaxTextBytes} byte limit.");
        }

        return body;
    }

    public static string ValidateHttpUrl(string body)
    {
        if (!Uri.TryCreate(body, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ValidationException("Link must be an absolute http or https URL.");
        }

        return uri.AbsoluteUri;
    }

    public static string ValidateFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("File name is required.");
        }

        var name = Path.GetFileName(fileName.Trim());
        if (name.Length is 0 or > RoomLimits.MaxFileNameLength)
        {
            throw new ValidationException("File name is invalid.");
        }

        return name;
    }
}
