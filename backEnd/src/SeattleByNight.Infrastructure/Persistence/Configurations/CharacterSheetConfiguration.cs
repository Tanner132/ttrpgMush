using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterSheetConfiguration : IEntityTypeConfiguration<CharacterSheet>
{
    public void Configure(EntityTypeBuilder<CharacterSheet> builder)
    {
        builder.ToTable("character_sheets", table =>
        {
            table.HasCheckConstraint("ck_character_sheets_schema_version", "sheet_schema_version > 0");
            table.HasCheckConstraint("ck_character_sheets_digests", "length(catalog_semantic_digest) = 64 AND length(source_draft_digest) = 64");
        });

        builder.HasKey(sheet => sheet.CharacterId);
        builder.Property(sheet => sheet.CharacterId).HasColumnName("character_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(sheet => sheet.RulesetId).HasColumnName("ruleset_id").HasMaxLength(50).IsRequired();
        builder.Property(sheet => sheet.CatalogVersion).HasColumnName("catalog_version").HasMaxLength(30).IsRequired();
        builder.Property(sheet => sheet.CatalogSemanticDigest).HasColumnName("catalog_semantic_digest").HasMaxLength(64).IsRequired();
        builder.Property(sheet => sheet.CreationMethodId).HasColumnName("creation_method_id").HasMaxLength(50).IsRequired();
        builder.Property(sheet => sheet.SheetSchemaVersion).HasColumnName("sheet_schema_version").IsRequired();
        builder.Property(sheet => sheet.CanonicalSheetJson).HasColumnName("canonical_sheet").HasColumnType("jsonb").IsRequired();
        builder.Property(sheet => sheet.SourceDraftDigest).HasColumnName("source_draft_digest").HasMaxLength(64).IsRequired();
        builder.Property(sheet => sheet.FinalizedAtUtc).HasColumnName("finalized_at_utc").HasColumnType("timestamp with time zone");

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterSheet>(sheet => sheet.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
