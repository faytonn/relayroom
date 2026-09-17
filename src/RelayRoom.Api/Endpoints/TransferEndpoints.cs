using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RelayRoom.Api.Auth;
using RelayRoom.Application.Contracts;
using RelayRoom.Application.Services;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;

namespace RelayRoom.Api.Endpoints;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rooms/{roomId:guid}/transfers", Create)
            .RequireAuthorization()
            .WithTags("Transfers");

        app.MapDelete("/api/rooms/{roomId:guid}/transfers/{transferId:guid}", Delete)
            .RequireAuthorization()
            .WithTags("Transfers");

        app.MapGet("/api/transfers/{transferId:guid}/download-url", DownloadUrl)
            .RequireAuthorization()
            .WithTags("Transfers");

        app.MapGet("/api/transfers/{transferId:guid}/content", Content)
            .RequireAuthorization()
            .WithTags("Transfers");

        app.MapPost("/api/transfers/{transferId:guid}/deliveries/ack", Ack)
            .RequireAuthorization()
            .WithTags("Transfers");

        return app;
    }

    private static async Task<Ok<TransferResponse>> Create(
        Guid roomId,
        CreateTransferRequest request,
        ITransferService transfers,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        user.EnsureRoom(roomId);
        var deviceId = user.GetDeviceId();

        var transfer = request.Kind is TransferKind.Text or TransferKind.Link
            ? await transfers.CreateInlineAsync(roomId, deviceId, request.Kind, request.TextBody, request.TargetDeviceId, cancellationToken)
            : await transfers.CreateUploadIntentAsync(
                roomId,
                deviceId,
                request.Kind,
                request.FileName,
                request.MimeType,
                request.SizeBytes ?? throw new ValidationException("sizeBytes is required for file transfers."),
                request.Sha256,
                request.TargetDeviceId,
                cancellationToken);

        return TypedResults.Ok(TransferResponse.From(transfer));
    }

    private static async Task<NoContent> Delete(
        Guid roomId,
        Guid transferId,
        ITransferService transfers,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        user.EnsureRoom(roomId);
        await transfers.DeleteAsync(roomId, user.GetDeviceId(), transferId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<DownloadUrlResponse>> DownloadUrl(
        Guid transferId,
        ITransferService transfers,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var fallback = $"{http.Request.Scheme}://{http.Request.Host}/api/transfers/{transferId}/content";
        var result = await transfers.CreateDownloadUrlAsync(transferId, http.User.GetDeviceId(), fallback, cancellationToken);
        return TypedResults.Ok(new DownloadUrlResponse(result.Url, result.ExpiresAt, result.Inline));
    }

    private static async Task<IResult> Content(
        Guid transferId,
        ITransferService transfers,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var (transfer, stream) = await transfers.OpenContentAsync(transferId, user.GetDeviceId(), cancellationToken);
        var fileName = transfer.FileName ?? (transfer.Kind is TransferKind.Link ? "link.txt" : "note.txt");
        return Results.File(stream, transfer.MimeType ?? "application/octet-stream", fileName);
    }

    private static async Task<Ok<TransferDeliveryResponse>> Ack(
        Guid transferId,
        AckDeliveryRequest request,
        ITransferService transfers,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var delivery = await transfers.AckAsync(transferId, user.GetDeviceId(), request.Status, request.ProgressBytes, cancellationToken);
        return TypedResults.Ok(TransferDeliveryResponse.From(delivery));
    }
}
