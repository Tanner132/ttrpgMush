using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence.Configurations;

namespace SeattleByNight.Infrastructure.Persistence;

public sealed class SeattleByNightDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public SeattleByNightDbContext(DbContextOptions<SeattleByNightDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomExit> RoomExits => Set<RoomExit>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();
    public DbSet<RoomVisit> RoomVisits => Set<RoomVisit>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeattleByNightDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Configure(modelBuilder);
    }
}
