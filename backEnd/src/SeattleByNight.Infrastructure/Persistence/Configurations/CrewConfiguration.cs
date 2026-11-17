using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CrewConfiguration : IEntityTypeConfiguration<Crew>
{
    public void Configure(EntityTypeBuilder<Crew> builder)
    {
        builder.ToTable("crews");
        builder.HasKey(crew => crew.Id);
        builder.Property(crew => crew.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(crew => crew.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(crew => crew.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
    }
}

public sealed class CrewInvitationConfiguration : IEntityTypeConfiguration<CrewInvitation>
{
    public void Configure(EntityTypeBuilder<CrewInvitation> builder)
    {
        builder.ToTable("crew_invitations");
        builder.HasKey(invite => invite.Id);
        builder.Property(invite => invite.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(invite => invite.InviterCharacterId).HasColumnName("inviter_character_id").HasColumnType("uuid").IsRequired();
        builder.Property(invite => invite.InviteeCharacterId).HasColumnName("invitee_character_id").HasColumnType("uuid").IsRequired();
        builder.Property(invite => invite.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(invite => invite.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(invite => invite.InviteeCharacterId).HasDatabaseName("ix_crew_invitations_invitee");

        builder.HasOne<Character>().WithMany().HasForeignKey(invite => invite.InviterCharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(invite => invite.InviteeCharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}
