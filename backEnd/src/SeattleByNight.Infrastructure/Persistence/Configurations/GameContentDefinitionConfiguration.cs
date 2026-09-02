using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class GameContentDefinitionConfiguration : IEntityTypeConfiguration<GameContentDefinition>
{
    public void Configure(EntityTypeBuilder<GameContentDefinition> builder)
    {
        builder.ToTable("game_content_definitions");

        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(definition => definition.Kind).HasColumnName("kind").HasMaxLength(40).IsRequired();
        builder.Property(definition => definition.ContentKey).HasColumnName("content_key").HasMaxLength(100).IsRequired();
        builder.Property(definition => definition.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(definition => definition.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(definition => definition.PublishedJson).HasColumnName("published_payload").HasColumnType("jsonb");
        builder.Property(definition => definition.DraftJson).HasColumnName("draft_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(definition => definition.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(definition => definition.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(definition => definition.PublishedAtUtc).HasColumnName("published_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(definition => definition.RetiredAtUtc).HasColumnName("retired_at_utc").HasColumnType("timestamp with time zone");

        // The authored id is the reference every mission instance, objective,
        // and scene effect is written against, so it is unique per kind at
        // the database level, not just in the loader's duplicate check.
        builder.HasIndex(definition => new { definition.Kind, definition.ContentKey })
            .IsUnique()
            .HasDatabaseName("ux_game_content_definitions_kind_key");

        // The provider's composition query: every published definition.
        builder.HasIndex(definition => definition.Status)
            .HasDatabaseName("ix_game_content_definitions_status");

        // A published definition must have a payload to serve, and a draft
        // one must not be pretending to have been published.
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_game_content_definitions_published_payload",
            "(status = 'Draft' AND published_payload IS NULL) OR (status <> 'Draft' AND published_payload IS NOT NULL)"));
    }
}
