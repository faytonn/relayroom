using Microsoft.EntityFrameworkCore;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<BlobObject> Blobs => Set<BlobObject>();
    public DbSet<TransferDelivery> Deliveries => Set<TransferDelivery>();
    public DbSet<RoomEvent> RoomEvents => Set<RoomEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
