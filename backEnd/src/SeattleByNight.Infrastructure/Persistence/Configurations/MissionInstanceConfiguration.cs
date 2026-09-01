using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class MissionInstanceConfiguration : IEntityTypeConfiguration<MissionInstance>
{
    public void Configure(EntityTypeBuilder<MissionInstance> builder)
    {
        builder.ToTable("mission_instances");

        builder.HasKey(instance => instance.Id);
        builder.Property(instance => instance.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(instance => instance.MissionId).HasColumnName("mission_id").HasMaxLength(100).IsRequired();
        builder.Property(instance => instance.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(instance => instance.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(instance => instance.ObjectivesJson).HasColumnName("objectives").HasColumnType("jsonb").IsRequired();
        builder.Property(instance => instance.NegotiatedNuyen).HasColumnName("negotiated_nuyen");
        builder.Property(instance => instance.AcceptedAtUtc).HasColumnName("accepted_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(instance => instance.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(instance => instance.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(instance => new { instance.CharacterId, instance.MissionId })
            .HasDatabaseName("ix_mission_instances_character_mission");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(instance => instance.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
