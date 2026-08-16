using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;
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

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(c => c.CurrentRoomId)
            .HasDatabaseName("ix_characters_current_room_id");

        builder.HasIndex(c => c.NormalizedName)
            .HasDatabaseName("ix_characters_normalized_name")
            .IsUnique();

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
