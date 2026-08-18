using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("characters");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.CurrentRoomId)
            .HasColumnName("current_room_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.LifecycleState)
            .HasColumnName("lifecycle_state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.FinalizedAtUtc)
            .HasColumnName("finalized_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(c => c.CurrentRoomId)
            .HasDatabaseName("ix_characters_current_room_id");

        builder.HasIndex(c => c.NormalizedName)
            .HasDatabaseName("ix_characters_normalized_name")
            .IsUnique();

        builder.HasIndex(c => new { c.UserId, c.LifecycleState })
            .HasDatabaseName("ix_characters_user_id_lifecycle_state");

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_characters_lifecycle_finalized_at",
            "(lifecycle_state = 'Draft' AND finalized_at_utc IS NULL) OR (lifecycle_state = 'Finalized' AND finalized_at_utc IS NOT NULL)"));

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(c => c.CurrentRoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
