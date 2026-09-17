using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicCode).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EncryptionMode).HasConversion<string>().HasMaxLength(32);
        // PostgreSQL folds unquoted identifiers to lowercase; the column is "Status".
        builder.HasIndex(x => x.PublicCode).IsUnique().HasFilter("\"Status\" = 'Active'");
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasMany(x => x.Devices).WithOne(x => x.Room).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Transfers).WithOne(x => x.Room).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Events).WithOne(x => x.Room).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.ConnectionId).HasMaxLength(128);
        builder.HasIndex(x => x.RoomId);
    }
}

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.MimeType).HasMaxLength(128);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.TextBody).HasMaxLength(22000);
        builder.HasIndex(x => new { x.RoomId, x.CreatedAt });
        builder.HasOne(x => x.Sender).WithMany(x => x.SentTransfers).HasForeignKey(x => x.SenderDeviceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetDeviceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Blob).WithOne(x => x.Transfer).HasForeignKey<Transfer>(x => x.BlobId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class BlobObjectConfiguration : IEntityTypeConfiguration<BlobObject>
{
    public void Configure(EntityTypeBuilder<BlobObject> builder)
    {
        builder.ToTable("blobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.UploadStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}

public sealed class TransferDeliveryConfiguration : IEntityTypeConfiguration<TransferDelivery>
{
    public void Configure(EntityTypeBuilder<TransferDelivery> builder)
    {
        builder.ToTable("transfer_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.TransferId, x.DeviceId }).IsUnique();
        builder.HasOne(x => x.Transfer).WithMany(x => x.Deliveries).HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Device).WithMany(x => x.Deliveries).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomEventConfiguration : IEntityTypeConfiguration<RoomEvent>
{
    public void Configure(EntityTypeBuilder<RoomEvent> builder)
    {
        builder.ToTable("room_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.RoomId, x.CreatedAt });
    }
}
