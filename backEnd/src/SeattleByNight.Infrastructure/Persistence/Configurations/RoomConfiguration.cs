using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(r => r.AccessType)
            .HasColumnName("access_type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.MapX)
            .HasColumnName("map_x")
            .IsRequired();

        builder.Property(r => r.MapY)
            .HasColumnName("map_y")
            .IsRequired();

        builder.Property(r => r.MapLayer)
            .HasColumnName("map_layer")
            .IsRequired();

        // WF-41: a room's place is unique within its map and layer.
        builder.Property(r => r.MapId)
            .HasColumnName("map_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(r => new { r.MapId, r.MapLayer, r.MapX, r.MapY })
            .IsUnique()
            .HasDatabaseName("ux_rooms_map_id_map_layer_map_x_map_y");

        builder.HasOne<CityMap>()
            .WithMany()
            .HasForeignKey(r => r.MapId)
            .OnDelete(DeleteBehavior.Restrict);

        // WF-60: draft or live.
        builder.Property(r => r.IsLive)
            .HasColumnName("is_live")
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.Version)
            .HasColumnName("version")
            .HasColumnType("uuid")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
    }
}
