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

        // Map coordinates are only meaningful (and only unique) on the shared
        // world map — instanced rooms all sit at 0/0/0 outside it (§31).
        builder.HasIndex(r => new { r.MapLayer, r.MapX, r.MapY })
            .IsUnique()
            .HasFilter("encounter_instance_id IS NULL")
            .HasDatabaseName("ux_rooms_map_layer_map_x_map_y");

        builder.Property(r => r.EncounterInstanceId)
            .HasColumnName("encounter_instance_id")
            .HasColumnType("uuid");

        builder.HasIndex(r => r.EncounterInstanceId)
            .HasDatabaseName("ix_rooms_encounter_instance");

        builder.HasOne<EncounterInstance>()
            .WithMany()
            .HasForeignKey(r => r.EncounterInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

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
