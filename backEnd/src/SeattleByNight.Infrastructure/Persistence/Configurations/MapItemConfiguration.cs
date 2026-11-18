using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

// WF-87: items the map places.
public sealed class MapItemConfiguration : IEntityTypeConfiguration<MapItem>
{
    public void Configure(EntityTypeBuilder<MapItem> builder)
    {
        builder.ToTable("map_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.RoomId).HasColumnName("room_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.ContainerId).HasColumnName("container_id").HasColumnType("uuid");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(item => item.IsUnique).HasColumnName("is_unique").IsRequired();
        builder.Property(item => item.RespawnMinutes).HasColumnName("respawn_minutes");
        builder.Property(item => item.IsHidden).HasColumnName("is_hidden").IsRequired();
        builder.Property(item => item.DiscoveryThreshold).HasColumnName("discovery_threshold").IsRequired();
        builder.Property(item => item.TakenAtUtc).HasColumnName("taken_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(item => item.RoomId).HasDatabaseName("ix_map_items_room");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(item => item.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoomInteractable>()
            .WithMany()
            .HasForeignKey(item => item.ContainerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
