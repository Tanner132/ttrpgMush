using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class PlaySessionConfiguration : IEntityTypeConfiguration<PlaySession>
{
    public void Configure(EntityTypeBuilder<PlaySession> builder)
    {
        builder.ToTable("play_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.CharacterId)
            .HasColumnName("character_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.StartAtUtc)
            .HasColumnName("start_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.LastActivityUtc)
            .HasColumnName("last_activity_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.EndedAtUtc)
            .HasColumnName("ended_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("ix_play_sessions_user_id_active")
            .IsUnique()
            .HasFilter("ended_at_utc IS NULL");

        builder.HasIndex(s => s.CharacterId)
            .HasDatabaseName("ix_play_sessions_character_id");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(s => s.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
