using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RelayRoom.Application.Persistence;
using RelayRoom.Core.Domain;
using RelayRoom.Persistence.Repositories;

namespace RelayRoom.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
