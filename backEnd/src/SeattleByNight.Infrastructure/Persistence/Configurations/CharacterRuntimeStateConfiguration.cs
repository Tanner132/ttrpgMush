using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CharacterRuntimeStateConfiguration : IEntityTypeConfiguration<CharacterRuntimeState>
{
    public void Configure(EntityTypeBuilder<CharacterRuntimeState> builder)
    {
        builder.ToTable("character_runtime_states", table =>
        {
            table.HasCheckConstraint(
                "ck_character_runtime_states_nonnegative",
                "physical_damage >= 0 AND stun_damage >= 0 AND current_edge >= 0");
        });

        builder.HasKey(state => state.CharacterId);
        builder.Property(state => state.CharacterId).HasColumnName("character_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(state => state.PhysicalDamage).HasColumnName("physical_damage").IsRequired();
        builder.Property(state => state.StunDamage).HasColumnName("stun_damage").IsRequired();
        builder.Property(state => state.CurrentEdge).HasColumnName("current_edge").IsRequired();
        builder.Property(state => state.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(state => state.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterRuntimeState>(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
