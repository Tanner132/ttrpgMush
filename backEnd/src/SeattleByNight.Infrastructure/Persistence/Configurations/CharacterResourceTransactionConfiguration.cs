using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterResourceTransactionConfiguration : IEntityTypeConfiguration<CharacterResourceTransaction>
{
    public void Configure(EntityTypeBuilder<CharacterResourceTransaction> builder)
    {
        builder.ToTable("character_resource_transactions", table =>
        {
            table.HasCheckConstraint("ck_character_resource_transactions_balance", "balance_after >= 0");
            table.HasCheckConstraint(
                "ck_character_resource_transactions_single_reference",
                "NOT (advancement_id IS NOT NULL AND inventory_item_id IS NOT NULL)");
        });

        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(transaction => transaction.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(transaction => transaction.ResourceType).HasColumnName("resource_type").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(transaction => transaction.Amount).HasColumnName("amount").IsRequired();
        builder.Property(transaction => transaction.BalanceAfter).HasColumnName("balance_after").IsRequired();
        builder.Property(transaction => transaction.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(transaction => transaction.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(transaction => transaction.AdvancementId).HasColumnName("advancement_id").HasColumnType("uuid");
        builder.Property(transaction => transaction.InventoryItemId).HasColumnName("inventory_item_id").HasColumnType("uuid");
        builder.Property(transaction => transaction.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(transaction => new { transaction.CharacterId, transaction.CreatedAtUtc });

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CharacterAdvancement>()
            .WithMany()
            .HasForeignKey(transaction => transaction.AdvancementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CharacterInventoryItem>()
            .WithMany()
            .HasForeignKey(transaction => transaction.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
