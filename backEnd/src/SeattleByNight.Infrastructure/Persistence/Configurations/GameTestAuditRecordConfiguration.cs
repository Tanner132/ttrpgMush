using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class GameTestAuditRecordConfiguration : IEntityTypeConfiguration<GameTestAuditRecord>
{
    public void Configure(EntityTypeBuilder<GameTestAuditRecord> builder)
    {
        builder.ToTable("game_test_audit_records");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(record => record.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(record => record.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.CharacterId).HasColumnName("character_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.RoomId).HasColumnName("room_id").HasColumnType("uuid");
        builder.Property(record => record.TestId).HasColumnName("test_id").HasMaxLength(100).IsRequired();
        builder.Property(record => record.RngSeed).HasColumnName("rng_seed").IsRequired();
        builder.Property(record => record.Success).HasColumnName("success").IsRequired();
        builder.Property(record => record.ResultJson).HasColumnName("result").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(record => new { record.CharacterId, record.CreatedAtUtc })
            .HasDatabaseName("ix_game_test_audit_records_character_created");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(record => record.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
