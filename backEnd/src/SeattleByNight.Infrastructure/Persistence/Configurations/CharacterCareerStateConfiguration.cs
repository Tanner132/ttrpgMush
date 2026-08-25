using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterCareerStateConfiguration : IEntityTypeConfiguration<CharacterCareerState>
{
    public void Configure(EntityTypeBuilder<CharacterCareerState> builder)
    {
        builder.ToTable("character_career_states", table =>
        {
            table.HasCheckConstraint("ck_character_career_states_schema_version", "career_document_schema_version > 0");
            table.HasCheckConstraint("ck_character_career_states_nonnegative", "current_karma >= 0 AND current_nuyen >= 0 AND lifetime_karma_earned >= 0");
        });

        builder.HasKey(state => state.CharacterId);
        builder.Property(state => state.CharacterId).HasColumnName("character_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(state => state.CareerDocumentSchemaVersion).HasColumnName("career_document_schema_version").IsRequired();
        builder.Property(state => state.ProgressionJson).HasColumnName("progression").HasColumnType("jsonb").IsRequired();
        builder.Property(state => state.CurrentKarma).HasColumnName("current_karma").IsRequired();
        builder.Property(state => state.CurrentNuyen).HasColumnName("current_nuyen").IsRequired();
        builder.Property(state => state.LifetimeKarmaEarned).HasColumnName("lifetime_karma_earned").IsRequired();
        builder.Property(state => state.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(state => state.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(state => state.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterCareerState>(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
