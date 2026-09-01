using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class NpcInstanceConfiguration : IEntityTypeConfiguration<NpcInstance>
{
    public void Configure(EntityTypeBuilder<NpcInstance> builder)
    {
        builder.ToTable("npc_instances");

        builder.HasKey(npc => npc.Id);
        builder.Property(npc => npc.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(npc => npc.TemplateId).HasColumnName("template_id").HasMaxLength(100).IsRequired();
        builder.Property(npc => npc.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(npc => npc.RoomId).HasColumnName("room_id").HasColumnType("uuid").IsRequired();
        builder.Property(npc => npc.PhysicalDamage).HasColumnName("physical_damage").IsRequired();
        builder.Property(npc => npc.StunDamage).HasColumnName("stun_damage").IsRequired();
        builder.Property(npc => npc.Awareness).HasColumnName("awareness").HasMaxLength(50).IsRequired();
        builder.Property(npc => npc.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(npc => npc.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(npc => npc.RoomId)
            .HasDatabaseName("ix_npc_instances_room");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(npc => npc.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
