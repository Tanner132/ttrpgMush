using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterCreationDraftConfiguration : IEntityTypeConfiguration<CharacterCreationDraft>
{
    public void Configure(EntityTypeBuilder<CharacterCreationDraft> builder)
    {
        builder.ToTable("character_creation_drafts", table =>
        {
            table.HasCheckConstraint("ck_character_creation_drafts_document_schema_version", "document_schema_version > 0");
            table.HasCheckConstraint("ck_character_creation_drafts_digest", "length(catalog_semantic_digest) = 64");
        });

        builder.HasKey(draft => draft.CharacterId);
        builder.Property(draft => draft.CharacterId).HasColumnName("character_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(draft => draft.RulesetId).HasColumnName("ruleset_id").HasMaxLength(50).IsRequired();
        builder.Property(draft => draft.CatalogVersion).HasColumnName("catalog_version").HasMaxLength(30).IsRequired();
        builder.Property(draft => draft.CatalogSemanticDigest).HasColumnName("catalog_semantic_digest").HasMaxLength(64).IsRequired();
        builder.Property(draft => draft.CreationMethodId).HasColumnName("creation_method_id").HasMaxLength(50).IsRequired();
        builder.Property(draft => draft.DocumentSchemaVersion).HasColumnName("document_schema_version").IsRequired();
        builder.Property(draft => draft.SelectionsJson).HasColumnName("selections").HasColumnType("jsonb").IsRequired();
        builder.Property(draft => draft.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(draft => draft.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(draft => draft.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterCreationDraft>(draft => draft.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
