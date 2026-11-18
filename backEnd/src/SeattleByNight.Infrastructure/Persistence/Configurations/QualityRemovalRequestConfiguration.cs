using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class QualityRemovalRequestConfiguration : IEntityTypeConfiguration<QualityRemovalRequest>
{
    public void Configure(EntityTypeBuilder<QualityRemovalRequest> builder)
    {
        builder.ToTable("quality_removal_requests", table =>
        {
            table.HasCheckConstraint("ck_quality_removal_requests_karma_cost", "karma_cost > 0");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(request => request.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(request => request.RequestedByUserId).HasColumnName("requested_by_user_id").HasColumnType("uuid").IsRequired();
        builder.Property(request => request.QualityKey).HasColumnName("quality_key").HasMaxLength(64).IsRequired();
        builder.Property(request => request.QualityId).HasColumnName("quality_id").HasMaxLength(128).IsRequired();
        builder.Property(request => request.KarmaCost).HasColumnName("karma_cost").IsRequired();
        builder.Property(request => request.Requirement).HasColumnName("requirement").HasMaxLength(500).IsRequired();
        builder.Property(request => request.Explanation).HasColumnName("explanation").HasMaxLength(2000).IsRequired();

        // The status is the concurrency token, so two Storytellers resolving
        // the same request cannot both win: the second UPDATE matches no row.
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(10)
            .IsRequired().IsConcurrencyToken();
        builder.Property(request => request.HoldTransactionId).HasColumnName("hold_transaction_id").HasColumnType("uuid").IsRequired();
        builder.Property(request => request.AdvancementId).HasColumnName("advancement_id").HasColumnType("uuid");
        builder.Property(request => request.RefundTransactionId).HasColumnName("refund_transaction_id").HasColumnType("uuid");
        builder.Property(request => request.ResolvedByUserId).HasColumnName("resolved_by_user_id").HasColumnType("uuid");
        builder.Property(request => request.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(2000);
        builder.Property(request => request.RequestedAtUtc).HasColumnName("requested_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(request => request.ResolvedAtUtc).HasColumnName("resolved_at_utc").HasColumnType("timestamp with time zone");

        // One open request per quality instance.
        builder.HasIndex(request => new { request.CharacterId, request.QualityKey })
            .IsUnique()
            .HasFilter($"status = '{nameof(QualityRemovalRequestStatus.Pending)}'");
        builder.HasIndex(request => new { request.Status, request.RequestedAtUtc });

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(request => request.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CharacterResourceTransaction>()
            .WithMany()
            .HasForeignKey(request => request.HoldTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CharacterResourceTransaction>()
            .WithMany()
            .HasForeignKey(request => request.RefundTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CharacterAdvancement>()
            .WithMany()
            .HasForeignKey(request => request.AdvancementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
