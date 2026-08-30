using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterActiveEffectConfiguration : IEntityTypeConfiguration<CharacterActiveEffect>
{
    public void Configure(EntityTypeBuilder<CharacterActiveEffect> builder)
    {
        builder.ToTable("character_active_effects");

        builder.HasKey(effect => effect.Id);
        builder.Property(effect => effect.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(effect => effect.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(effect => effect.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
        builder.Property(effect => effect.SourceId).HasColumnName("source_id").HasMaxLength(100).IsRequired();
        builder.Property(effect => effect.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(effect => effect.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(effect => effect.DurationType).HasColumnName("duration_type").HasMaxLength(50).IsRequired();
        builder.Property(effect => effect.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(effect => effect.StackingRule).HasColumnName("stacking_rule").HasMaxLength(50).IsRequired();
        builder.Property(effect => effect.StackingGroup).HasColumnName("stacking_group").HasMaxLength(100);
        builder.Property(effect => effect.AppliedAtUtc).HasColumnName("applied_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(effect => effect.CharacterId)
            .HasDatabaseName("ix_character_active_effects_character");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(effect => effect.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
