using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class EncounterInstanceConfiguration : IEntityTypeConfiguration<EncounterInstance>
{
    public void Configure(EntityTypeBuilder<EncounterInstance> builder)
    {
        builder.ToTable("encounter_instances");

        builder.HasKey(instance => instance.Id);
        builder.Property(instance => instance.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(instance => instance.EncounterId).HasColumnName("encounter_id").HasMaxLength(100).IsRequired();
        builder.Property(instance => instance.MissionInstanceId).HasColumnName("mission_instance_id").HasColumnType("uuid").IsRequired();
        builder.Property(instance => instance.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(instance => instance.EntryRoomId).HasColumnName("entry_room_id").HasColumnType("uuid").IsRequired();
        builder.Property(instance => instance.ReturnRoomId).HasColumnName("return_room_id").HasColumnType("uuid").IsRequired();
        builder.Property(instance => instance.LastActivityUtc).HasColumnName("last_activity_utc").HasColumnType("timestamp with time zone");
        builder.Property(instance => instance.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(instance => instance.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(instance => instance.MissionInstanceId)
            .HasDatabaseName("ix_encounter_instances_mission_instance");

        builder.HasIndex(instance => instance.Status)
            .HasDatabaseName("ix_encounter_instances_status");

        builder.HasOne<MissionInstance>()
            .WithMany()
            .HasForeignKey(instance => instance.MissionInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // EntryRoomId/ReturnRoomId are deliberately NOT foreign keys: the
        // entry room is created in the same commit as this row (a circular
        // dependency with rooms.encounter_instance_id), and both are archival
        // pointers once the instance is terminal.
    }
}
