using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class RoomExitConfiguration : IEntityTypeConfiguration<RoomExit>
{
    public void Configure(EntityTypeBuilder<RoomExit> builder)
    {
        builder.ToTable("room_exits", table => table.HasCheckConstraint(
            "ck_room_exits_direction",
            "direction IN ('north','northeast','east','southeast','south','southwest','west','northwest','up','down')"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.SourceRoomId)
            .HasColumnName("source_room_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.DestinationRoomId)
            .HasColumnName("destination_room_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Direction)
            .HasColumnName("direction")
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(e => e.IsHidden)
            .HasColumnName("is_hidden");

        builder.Property(e => e.IsLocked)
            .HasColumnName("is_locked");

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasColumnType("uuid")
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.HasIndex(e => new { e.SourceRoomId, e.Direction })
            .IsUnique()
            .HasDatabaseName("ux_room_exits_source_room_id_direction");

        builder.HasIndex(e => e.DestinationRoomId)
            .HasDatabaseName("ix_room_exits_destination_room_id");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(e => e.SourceRoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(e => e.DestinationRoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
