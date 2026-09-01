using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class EncounterParticipantConfiguration : IEntityTypeConfiguration<EncounterParticipant>
{
    public void Configure(EntityTypeBuilder<EncounterParticipant> builder)
    {
        builder.ToTable("encounter_participants");

        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(participant => participant.EncounterInstanceId).HasColumnName("encounter_instance_id").HasColumnType("uuid").IsRequired();
        builder.Property(participant => participant.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(participant => participant.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(participant => new { participant.EncounterInstanceId, participant.CharacterId })
            .IsUnique()
            .HasDatabaseName("ux_encounter_participants_instance_character");

        builder.HasIndex(participant => participant.CharacterId)
            .HasDatabaseName("ix_encounter_participants_character");

        builder.HasOne<EncounterInstance>()
            .WithMany()
            .HasForeignKey(participant => participant.EncounterInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(participant => participant.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
