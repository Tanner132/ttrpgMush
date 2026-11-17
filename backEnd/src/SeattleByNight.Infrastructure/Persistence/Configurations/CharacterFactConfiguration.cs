using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

// WF-80: one row per fact a character has.
public sealed class CharacterFactConfiguration : IEntityTypeConfiguration<CharacterFact>
{
    public void Configure(EntityTypeBuilder<CharacterFact> builder)
    {
        builder.ToTable("character_facts");
        builder.HasKey(fact => new { fact.CharacterId, fact.FactId });
        builder.Property(fact => fact.CharacterId).HasColumnName("character_id");
        builder.Property(fact => fact.FactId).HasColumnName("fact_id");
        builder.Property(fact => fact.MissionInstanceId).HasColumnName("mission_instance_id");
        builder.Property(fact => fact.SetAtUtc).HasColumnName("set_at_utc");
        builder.HasIndex(fact => fact.FactId).HasDatabaseName("ix_character_facts_fact_id");
        builder.HasIndex(fact => fact.MissionInstanceId).HasDatabaseName("ix_character_facts_mission_instance_id");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(fact => fact.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MissionInstance>()
            .WithMany()
            .HasForeignKey(fact => fact.MissionInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
