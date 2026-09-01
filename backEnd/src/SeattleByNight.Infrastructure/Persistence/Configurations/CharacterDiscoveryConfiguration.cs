using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterDiscoveryConfiguration : IEntityTypeConfiguration<CharacterDiscovery>
{
    public void Configure(EntityTypeBuilder<CharacterDiscovery> builder)
    {
        builder.ToTable("character_discoveries");

        builder.HasKey(discovery => discovery.Id);
        builder.Property(discovery => discovery.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(discovery => discovery.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(discovery => discovery.SubjectType).HasColumnName("subject_type").HasMaxLength(50).IsRequired();
        builder.Property(discovery => discovery.SubjectId).HasColumnName("subject_id").HasColumnType("uuid").IsRequired();
        builder.Property(discovery => discovery.DiscoveredAtUtc).HasColumnName("discovered_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(discovery => new { discovery.CharacterId, discovery.SubjectType, discovery.SubjectId })
            .IsUnique()
            .HasDatabaseName("ix_character_discoveries_character_subject");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(discovery => discovery.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
