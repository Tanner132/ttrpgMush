using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class TriggerFireConfiguration : IEntityTypeConfiguration<TriggerFire>
{
    public void Configure(EntityTypeBuilder<TriggerFire> builder)
    {
        builder.ToTable("trigger_fires");

        builder.HasKey(fire => fire.Id);
        builder.Property(fire => fire.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(fire => fire.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(fire => fire.MissionInstanceId).HasColumnName("mission_instance_id").HasColumnType("uuid").IsRequired();
        builder.Property(fire => fire.TriggerKey).HasColumnName("trigger_key").HasMaxLength(100).IsRequired();
        builder.Property(fire => fire.FiredAtUtc).HasColumnName("fired_at_utc").HasColumnType("timestamp with time zone");

        // The uniqueness IS the fire-once rule: a duplicate insert is how a
        // race between two reactions loses rather than double-firing.
        builder.HasIndex(fire => new { fire.CharacterId, fire.MissionInstanceId, fire.TriggerKey })
            .IsUnique()
            .HasDatabaseName("ux_trigger_fires_character_mission_key");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(fire => fire.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mission instances are history: a fired trigger is kept as long as
        // the run it belongs to is.
        builder.HasOne<MissionInstance>()
            .WithMany()
            .HasForeignKey(fire => fire.MissionInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
