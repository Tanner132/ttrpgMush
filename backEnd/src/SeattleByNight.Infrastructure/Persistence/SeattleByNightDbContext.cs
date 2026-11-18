using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Domain;
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
    public DbSet<QualityRemovalRequest> QualityRemovalRequests => Set<QualityRemovalRequest>();
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
    public DbSet<CharacterFact> CharacterFacts => Set<CharacterFact>();
    public DbSet<MapItem> MapItems => Set<MapItem>();
    public DbSet<JobState> JobStates => Set<JobState>();
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<CrewInvitation> CrewInvitations => Set<CrewInvitation>();
    public DbSet<WorldItemInstance> WorldItemInstances => Set<WorldItemInstance>();
    public DbSet<SceneSession> SceneSessions => Set<SceneSession>();
    public DbSet<GameContentDefinition> GameContentDefinitions => Set<GameContentDefinition>();
    public DbSet<TriggerFire> TriggerFires => Set<TriggerFire>();
    public DbSet<CityMap> Maps => Set<CityMap>();
    public DbSet<TravelLink> TravelLinks => Set<TravelLink>();
    public DbSet<District> Districts => Set<District>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeattleByNightDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Configure(modelBuilder);
    }
}
