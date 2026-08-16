using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public static class IdentityModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureUserClaim(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureUserLogin(modelBuilder);
        ConfigureUserToken(modelBuilder);
        ConfigureRoleClaim(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ApplicationUser>();

        builder.ToTable("asp_net_users");

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(u => u.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.EmailConfirmed)
            .HasColumnName("email_confirmed");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash");

        builder.Property(u => u.SecurityStamp)
            .HasColumnName("security_stamp");

        builder.Property(u => u.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp");

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number");

        builder.Property(u => u.PhoneNumberConfirmed)
            .HasColumnName("phone_number_confirmed");

        builder.Property(u => u.TwoFactorEnabled)
            .HasColumnName("two_factor_enabled");

        builder.Property(u => u.LockoutEnd)
            .HasColumnName("lockout_end")
            .HasColumnType("timestamp with time zone");

        builder.Property(u => u.LockoutEnabled)
            .HasColumnName("lockout_enabled");

        builder.Property(u => u.AccessFailedCount)
            .HasColumnName("access_failed_count");

        builder.HasIndex(u => u.NormalizedUserName)
            .HasDatabaseName("ix_users_normalized_user_name")
            .IsUnique();

        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("ix_users_normalized_email")
            .IsUnique();
    }

    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityRole<Guid>>();

        builder.ToTable("asp_net_roles");

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(256);

        builder.Property(r => r.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(256);

        builder.Property(r => r.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp");

        builder.HasIndex(r => r.NormalizedName)
            .HasDatabaseName("ix_roles_normalized_name")
            .IsUnique();
    }

    private static void ConfigureUserClaim(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityUserClaim<Guid>>();

        builder.ToTable("asp_net_user_claims");

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(c => c.ClaimType)
            .HasColumnName("claim_type");

        builder.Property(c => c.ClaimValue)
            .HasColumnName("claim_value");
    }

    private static void ConfigureUserRole(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityUserRole<Guid>>();

        builder.ToTable("asp_net_user_roles");

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(r => r.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("uuid");
    }

    private static void ConfigureUserLogin(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityUserLogin<Guid>>();

        builder.ToTable("asp_net_user_logins");

        builder.Property(l => l.LoginProvider)
            .HasColumnName("login_provider");

        builder.Property(l => l.ProviderKey)
            .HasColumnName("provider_key");

        builder.Property(l => l.ProviderDisplayName)
            .HasColumnName("provider_display_name");

        builder.Property(l => l.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");
    }

    private static void ConfigureUserToken(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityUserToken<Guid>>();

        builder.ToTable("asp_net_user_tokens");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(t => t.LoginProvider)
            .HasColumnName("login_provider");

        builder.Property(t => t.Name)
            .HasColumnName("name");

        builder.Property(t => t.Value)
            .HasColumnName("value");
    }

    private static void ConfigureRoleClaim(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityRoleClaim<Guid>>();

        builder.ToTable("asp_net_role_claims");

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("uuid");

        builder.Property(c => c.ClaimType)
            .HasColumnName("claim_type");

        builder.Property(c => c.ClaimValue)
            .HasColumnName("claim_value");
    }
}
