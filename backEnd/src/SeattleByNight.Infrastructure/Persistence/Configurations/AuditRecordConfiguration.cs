using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(a => a.ActorUserId)
            .HasColumnName("actor_user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TargetId)
            .HasColumnName("target_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnName("details")
            .HasMaxLength(2000);

        builder.HasIndex(a => new { a.CreatedAtUtc, a.Id })
            .HasDatabaseName("ix_audit_records_created_at_utc_id");

        builder.HasIndex(a => a.ActorUserId)
            .HasDatabaseName("ix_audit_records_actor_user_id");

        builder.HasIndex(a => new { a.TargetType, a.TargetId })
            .HasDatabaseName("ix_audit_records_target");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
