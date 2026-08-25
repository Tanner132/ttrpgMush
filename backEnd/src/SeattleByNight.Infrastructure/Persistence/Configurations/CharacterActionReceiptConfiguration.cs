using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterActionReceiptConfiguration : IEntityTypeConfiguration<CharacterActionReceipt>
{
    public void Configure(EntityTypeBuilder<CharacterActionReceipt> builder)
    {
        builder.ToTable("character_action_receipts");

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(receipt => receipt.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(receipt => receipt.RequestId).HasColumnName("request_id").HasColumnType("uuid").IsRequired();
        builder.Property(receipt => receipt.ResultJson).HasColumnName("result").HasColumnType("jsonb").IsRequired();
        builder.Property(receipt => receipt.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(receipt => new { receipt.CharacterId, receipt.RequestId }).IsUnique();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(receipt => receipt.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
