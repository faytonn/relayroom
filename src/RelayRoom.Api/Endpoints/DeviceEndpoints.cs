using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RelayRoom.Api.Auth;
using RelayRoom.Application.Contracts;
using RelayRoom.Application.Services;

namespace RelayRoom.Api.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/devices/me", Rename)
            .RequireAuthorization()
            .WithTags("Devices");
        return app;
    }

    private static async Task<Ok<DeviceResponse>> Rename(
        RenameDeviceRequest request,
        IRoomSessionService rooms,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var device = await rooms.RenameAsync(user.GetRoomId(), user.GetDeviceId(), request.DisplayName, cancellationToken);
        return TypedResults.Ok(DeviceResponse.From(device));
    }
}
