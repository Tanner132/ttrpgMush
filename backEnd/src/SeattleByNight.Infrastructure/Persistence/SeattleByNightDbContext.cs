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
    public DbSet<CharacterCreationDraft> CharacterCreationDrafts => Set<CharacterCreationDraft>();
    public DbSet<CharacterSheet> CharacterSheets => Set<CharacterSheet>();
    public DbSet<CharacterCareerState> CharacterCareerStates => Set<CharacterCareerState>();
    public DbSet<CharacterResourceTransaction> CharacterResourceTransactions => Set<CharacterResourceTransaction>();
    public DbSet<CharacterAdvancement> CharacterAdvancements => Set<CharacterAdvancement>();
    public DbSet<CharacterInventoryItem> CharacterInventoryItems => Set<CharacterInventoryItem>();
    public DbSet<CharacterActionReceipt> CharacterActionReceipts => Set<CharacterActionReceipt>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();
    public DbSet<RoomVisit> RoomVisits => Set<RoomVisit>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<CharacterRuntimeState> CharacterRuntimeStates => Set<CharacterRuntimeState>();
    public DbSet<GameTestAuditRecord> GameTestAuditRecords => Set<GameTestAuditRecord>();
    public DbSet<CharacterActiveEffect> CharacterActiveEffects => Set<CharacterActiveEffect>();
    public DbSet<NpcInstance> NpcInstances => Set<NpcInstance>();
    public DbSet<RoomInteractable> RoomInteractables => Set<RoomInteractable>();
    public DbSet<CharacterDiscovery> CharacterDiscoveries => Set<CharacterDiscovery>();
    public DbSet<MissionInstance> MissionInstances => Set<MissionInstance>();
    public DbSet<EncounterInstance> EncounterInstances => Set<EncounterInstance>();
    public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();
    public DbSet<WorldItemInstance> WorldItemInstances => Set<WorldItemInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeattleByNightDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Configure(modelBuilder);
    }
}
