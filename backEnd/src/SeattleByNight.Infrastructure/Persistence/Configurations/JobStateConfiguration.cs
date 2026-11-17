using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class JobStateConfiguration : IEntityTypeConfiguration<JobState>
{
    public void Configure(EntityTypeBuilder<JobState> builder)
    {
        builder.ToTable("job_states", table => table.HasCheckConstraint(
            "ck_job_states_status", "status IN ('Available','Open','Aftermath')"));

        builder.HasKey(job => job.MissionId);
        builder.Property(job => job.MissionId).HasColumnName("mission_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(job => job.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(job => job.OpenedAtUtc).HasColumnName("opened_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(job => job.DeadlineAtUtc).HasColumnName("deadline_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(job => job.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(job => job.CompletedByCharacterId).HasColumnName("completed_by_character_id").HasColumnType("uuid");
        builder.Property(job => job.FirstAcceptGroupId).HasColumnName("first_accept_group_id").HasColumnType("uuid");
        builder.Property(job => job.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(job => job.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(job => job.Status).HasDatabaseName("ix_job_states_status");
    }
}
