using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterInventoryItemConfiguration : IEntityTypeConfiguration<CharacterInventoryItem>
{
    public void Configure(EntityTypeBuilder<CharacterInventoryItem> builder)
    {
        builder.ToTable("character_inventory_items", table =>
        {
            table.HasCheckConstraint("ck_character_inventory_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_character_inventory_items_price", "purchase_price_nuyen >= 0");
            table.HasCheckConstraint("ck_character_inventory_items_digest", "length(catalog_semantic_digest) = 64");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.CatalogItemId).HasColumnName("catalog_item_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.CatalogCollection).HasColumnName("catalog_collection").HasMaxLength(50).IsRequired();
        builder.Property(item => item.RulesetId).HasColumnName("ruleset_id").HasMaxLength(50).IsRequired();
        builder.Property(item => item.CatalogVersion).HasColumnName("catalog_version").HasMaxLength(30).IsRequired();
        builder.Property(item => item.CatalogSemanticDigest).HasColumnName("catalog_semantic_digest").HasMaxLength(64).IsRequired();
        builder.Property(item => item.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(item => item.Rating).HasColumnName("rating");
        builder.Property(item => item.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
        builder.Property(item => item.PurchasePriceNuyen).HasColumnName("purchase_price_nuyen").IsRequired();
        builder.Property(item => item.AcquisitionSource).HasColumnName("acquisition_source").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.AcquiredAtUtc).HasColumnName("acquired_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(item => new { item.CharacterId, item.AcquiredAtUtc });

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
