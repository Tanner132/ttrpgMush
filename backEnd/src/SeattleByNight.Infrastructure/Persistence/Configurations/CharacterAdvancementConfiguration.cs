using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterAdvancementConfiguration : IEntityTypeConfiguration<CharacterAdvancement>
{
    public void Configure(EntityTypeBuilder<CharacterAdvancement> builder)
    {
        builder.ToTable("character_advancements", table =>
        {
            table.HasCheckConstraint("ck_character_advancements_karma_cost", "karma_cost >= 0");
        });

        builder.HasKey(advancement => advancement.Id);
        builder.Property(advancement => advancement.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(advancement => advancement.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(advancement => advancement.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(advancement => advancement.TargetId).HasColumnName("target_id").HasMaxLength(100).IsRequired();
        builder.Property(advancement => advancement.DetailsJson).HasColumnName("details").HasColumnType("jsonb").IsRequired();
        builder.Property(advancement => advancement.PreviousValue).HasColumnName("previous_value");
        builder.Property(advancement => advancement.NewValue).HasColumnName("new_value");
        builder.Property(advancement => advancement.KarmaCost).HasColumnName("karma_cost").IsRequired();
        builder.Property(advancement => advancement.RulesetId).HasColumnName("ruleset_id").HasMaxLength(50).IsRequired();
        builder.Property(advancement => advancement.CatalogVersion).HasColumnName("catalog_version").HasMaxLength(30).IsRequired();
        builder.Property(advancement => advancement.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(advancement => new { advancement.CharacterId, advancement.CreatedAtUtc });

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(advancement => advancement.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
