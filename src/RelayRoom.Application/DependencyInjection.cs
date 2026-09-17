using Microsoft.Extensions.DependencyInjection;

namespace RelayRoom.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Services.IRoomSessionService, Services.RoomSessionService>();
        services.AddScoped<Services.ITransferService, Services.TransferService>();
        services.AddScoped<Services.IRoomCleanupService, Services.RoomCleanupService>();
        return services;
    }
}
