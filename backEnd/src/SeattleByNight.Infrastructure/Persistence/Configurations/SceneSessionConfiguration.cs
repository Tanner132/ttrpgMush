using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class SceneSessionConfiguration : IEntityTypeConfiguration<SceneSession>
{
    public void Configure(EntityTypeBuilder<SceneSession> builder)
    {
        builder.ToTable("scene_sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(session => session.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(session => session.NpcInstanceId).HasColumnName("npc_instance_id").HasColumnType("uuid");
        builder.Property(session => session.RoomId).HasColumnName("room_id").HasColumnType("uuid").IsRequired();
        builder.Property(session => session.SceneId).HasColumnName("scene_id").HasMaxLength(100).IsRequired();
        builder.Property(session => session.CurrentNodeId).HasColumnName("current_node_id").HasMaxLength(100).IsRequired();
        builder.Property(session => session.PendingNegotiatedNuyen).HasColumnName("pending_negotiated_nuyen");
        builder.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(session => session.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        // One conversation per character (§37 MVP): starting a new scene
        // replaces the old row.
        builder.HasIndex(session => session.CharacterId)
            .IsUnique()
            .HasDatabaseName("ux_scene_sessions_character");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(session => session.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<NpcInstance>()
            .WithMany()
            .HasForeignKey(session => session.NpcInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
