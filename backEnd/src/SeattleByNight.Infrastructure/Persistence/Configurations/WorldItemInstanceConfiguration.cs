using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class WorldItemInstanceConfiguration : IEntityTypeConfiguration<WorldItemInstance>
{
    public void Configure(EntityTypeBuilder<WorldItemInstance> builder)
    {
        builder.ToTable("world_item_instances", table =>
        {
            // §38: an item is never both lying somewhere and carried.
            // Workstream 8: it may be neither — out of play until handed over
            // or put back by its job's reset.
            table.HasCheckConstraint(
                "ck_world_item_instances_one_location",
                "room_id IS NULL OR owner_character_id IS NULL");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.ItemId).HasColumnName("item_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.IsHidden).HasColumnName("is_hidden");
        builder.Property(item => item.DiscoveryThreshold).HasColumnName("discovery_threshold");
        builder.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(item => item.MissionId).HasColumnName("mission_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.RoomId).HasColumnName("room_id").HasColumnType("uuid");
        builder.Property(item => item.OwnerCharacterId).HasColumnName("owner_character_id").HasColumnType("uuid");
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(item => item.RoomId)
            .HasDatabaseName("ix_world_item_instances_room");

        builder.HasIndex(item => item.OwnerCharacterId)
            .HasDatabaseName("ix_world_item_instances_owner");

        // One row per job item.
        builder.HasIndex(item => item.ItemId)
            .IsUnique()
            .HasDatabaseName("ux_world_item_instances_item");

        builder.HasIndex(item => item.MissionId)
            .HasDatabaseName("ix_world_item_instances_mission");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(item => item.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.OwnerCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
