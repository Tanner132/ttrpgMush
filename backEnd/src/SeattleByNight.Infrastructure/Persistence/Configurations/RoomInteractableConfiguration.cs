using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class RoomInteractableConfiguration : IEntityTypeConfiguration<RoomInteractable>
{
    public void Configure(EntityTypeBuilder<RoomInteractable> builder)
    {
        builder.ToTable("room_interactables");

        builder.HasKey(interactable => interactable.Id);
        builder.Property(interactable => interactable.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(interactable => interactable.RoomId).HasColumnName("room_id").HasColumnType("uuid").IsRequired();
        builder.Property(interactable => interactable.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(interactable => interactable.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(interactable => interactable.IsHidden).HasColumnName("is_hidden").IsRequired();
        builder.Property(interactable => interactable.DiscoveryThreshold).HasColumnName("discovery_threshold").IsRequired();
        builder.Property(interactable => interactable.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(interactable => interactable.RoomId)
            .HasDatabaseName("ix_room_interactables_room");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(interactable => interactable.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
