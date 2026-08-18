using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class RoomVisitConfiguration : IEntityTypeConfiguration<RoomVisit>
{
    public void Configure(EntityTypeBuilder<RoomVisit> builder)
    {
        builder.ToTable("room_visits", table => table.HasCheckConstraint(
            "ck_room_visits_interval",
            "left_at_utc IS NULL OR left_at_utc >= entered_at_utc"));

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(v => v.PlaySessionId)
            .HasColumnName("play_session_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(v => v.RoomId)
            .HasColumnName("room_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(v => v.EnteredAtUtc)
            .HasColumnName("entered_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(v => v.LeftAtUtc)
            .HasColumnName("left_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(v => v.PlaySessionId)
            .HasDatabaseName("ix_room_visits_play_session_id_open")
            .IsUnique()
            .HasFilter("left_at_utc IS NULL");

        builder.HasIndex(v => new { v.RoomId, v.EnteredAtUtc })
            .HasDatabaseName("ix_room_visits_room_id_entered_at_utc");

        builder.HasIndex(v => new { v.PlaySessionId, v.RoomId, v.EnteredAtUtc, v.LeftAtUtc })
            .HasDatabaseName("ix_room_visits_transcript_visibility");

        builder.HasOne<PlaySession>()
            .WithMany()
            .HasForeignKey(v => v.PlaySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(v => v.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
