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
            // §38: an item is either placed in a room or carried — never both,
            // never neither.
            table.HasCheckConstraint(
                "ck_world_item_instances_one_location",
                "(room_id IS NULL) <> (owner_character_id IS NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.ItemKey).HasColumnName("item_key").HasMaxLength(100).IsRequired();
        builder.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(item => item.MissionInstanceId).HasColumnName("mission_instance_id").HasColumnType("uuid");
        builder.Property(item => item.EncounterInstanceId).HasColumnName("encounter_instance_id").HasColumnType("uuid");
        builder.Property(item => item.RoomId).HasColumnName("room_id").HasColumnType("uuid");
        builder.Property(item => item.OwnerCharacterId).HasColumnName("owner_character_id").HasColumnType("uuid");
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(item => item.RoomId)
            .HasDatabaseName("ix_world_item_instances_room");

        builder.HasIndex(item => item.OwnerCharacterId)
            .HasDatabaseName("ix_world_item_instances_owner");

        builder.HasIndex(item => item.MissionInstanceId)
            .HasDatabaseName("ix_world_item_instances_mission_instance");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(item => item.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.OwnerCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MissionInstance>()
            .WithMany()
            .HasForeignKey(item => item.MissionInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EncounterInstance>()
            .WithMany()
            .HasForeignKey(item => item.EncounterInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
