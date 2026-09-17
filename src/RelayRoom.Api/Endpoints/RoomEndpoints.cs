using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RelayRoom.Api.Auth;
using RelayRoom.Application.Contracts;
using RelayRoom.Application.Services;
using RelayRoom.Core.Domain;

namespace RelayRoom.Api.Endpoints;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rooms", Create)
            .AllowAnonymous()
            .WithName("CreateRoom")
            .WithTags("Rooms");

        app.MapPost("/api/rooms/join", Join)
            .AllowAnonymous()
            .RequireRateLimiting("join")
            .WithName("JoinRoom")
            .WithTags("Rooms");

        app.MapGet("/api/rooms/{roomId:guid}", Get).RequireAuthorization().WithTags("Rooms");
        var rooms = app.MapGroup("/api/rooms/{roomId:guid}").RequireAuthorization().WithTags("Rooms");
        rooms.MapPost("/close", Close);
        rooms.MapGet("/devices", ListDevices);
        rooms.MapGet("/transfers", ListTransfers);
        return app;
    }

    private static async Task<Ok<RoomSessionResponse>> Create(
        CreateRoomRequest? request,
        IRoomSessionService rooms,
        HttpContext http,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var result = await rooms.CreateAsync(
            request?.DisplayName,
            http.Request.Headers.UserAgent.ToString(),
            PublicBaseUrl(http, config),
            cancellationToken);

        return TypedResults.Ok(ToSession(result));
    }

    private static async Task<Ok<RoomSessionResponse>> Join(
        JoinRoomRequest request,
        IRoomSessionService rooms,
        HttpContext http,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var result = await rooms.JoinAsync(
            request.Code,
            request.DisplayName,
            http.Request.Headers.UserAgent.ToString(),
            PublicBaseUrl(http, config),
            cancellationToken);

        return TypedResults.Ok(ToSession(result));
    }

    private static async Task<Ok<RoomDetailResponse>> Get(
        Guid roomId,
        IRoomSessionService rooms,
        HttpContext http,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        http.User.EnsureRoom(roomId);
        var detail = await rooms.GetAsync(roomId, http.User.GetDeviceId(), cancellationToken);
        var joinUrl = $"{PublicBaseUrl(http, config).TrimEnd('/')}/r/{detail.Room.PublicCode}";
        return TypedResults.Ok(new RoomDetailResponse(
            RoomResponse.From(detail.Room, joinUrl),
            detail.Devices.Select(DeviceResponse.From).ToList(),
            detail.Transfers.Select(TransferResponse.From).ToList()));
    }

    private static async Task<NoContent> Close(Guid roomId, IRoomSessionService rooms, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        user.EnsureRoom(roomId);
        await rooms.CloseAsync(roomId, user.GetDeviceId(), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<DeviceResponse>>> ListDevices(Guid roomId, IRoomSessionService rooms, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        user.EnsureRoom(roomId);
        var detail = await rooms.GetAsync(roomId, user.GetDeviceId(), cancellationToken);
        return TypedResults.Ok<IReadOnlyList<DeviceResponse>>(detail.Devices.Select(DeviceResponse.From).ToList());
    }

    private static async Task<Ok<IReadOnlyList<TransferResponse>>> ListTransfers(Guid roomId, IRoomSessionService rooms, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        user.EnsureRoom(roomId);
        var detail = await rooms.GetAsync(roomId, user.GetDeviceId(), cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TransferResponse>>(detail.Transfers.Select(TransferResponse.From).ToList());
    }

    private static RoomSessionResponse ToSession(RoomSessionResult result) =>
        new(
            RoomResponse.From(result.Room, result.JoinUrl),
            DeviceResponse.From(result.Device),
            result.Token.AccessToken,
            result.Token.ExpiresAt);

    private static string PublicBaseUrl(HttpContext http, IConfiguration config) =>
        config["RelayRoom:PublicBaseUrl"]
        ?? $"{http.Request.Scheme}://{http.Request.Host}";
}
