using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 (§50): the database-backed content pipeline — the embedded
// bundle imported as the first published content set, the provider composing
// what the game serves out of published rows only, and the publish gate
// refusing content the loader would reject.
public sealed class GameContentPipelineTests : IAsyncLifetime
{
    private static readonly Guid MissionKey = ShippedContentIds.Missions.GangWarehouseRetrieval;

    // Content writes are audited against a real account, so the fixture edits
    // as the seeded dev administrator rather than an invented id.
    private static readonly Guid Actor = DevelopmentDataSeeder.DevUserId;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _connectionString = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
        await GameContentSeeder.SeedAsync(db);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PlaySessionOptions());
        services.AddApplication();
        services.AddInfrastructure(_connectionString);
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Seeding_ImportsTheEmbeddedBundleAsPublishedContentAndIsIdempotent()
    {
        var expected = GameContentComposer.Split(EmbeddedGameContentProvider.ReadMergedJson());

        await using var db = CreateDbContext();
        var rows = await db.GameContentDefinitions.AsNoTracking().ToListAsync();

        Assert.Equal(expected.Count, rows.Count);
        Assert.All(rows, row => Assert.Equal(nameof(GameContentStatus.Published), row.Status));
        Assert.All(rows, row => Assert.NotNull(row.PublishedJson));
        Assert.Contains(rows, row =>
            row.Kind == nameof(GameContentKind.Mission) && row.ContentKey == MissionKey);

        // A second import adds nothing — the bundle seeds the store, it does
        // not overwrite it on every restart.
        Assert.Equal(0, await GameContentSeeder.SeedAsync(db));
    }

    [Fact]
    public void TheGameIsServedByTheDatabaseProvider_WithTheSameContentAsTheBundle()
    {
        var content = _provider.GetRequiredService<IGameContentProvider>();
        var embedded = new EmbeddedGameContentProvider().Current;

        Assert.IsType<DatabaseGameContentProvider>(content);
        // Membership, not order: the store composes by content key, so the
        // document no longer inherits the authored files' declaration order.
        Assert.Equal(
            Keys(embedded.Encounters.Select(encounter => encounter.Id)),
            Keys(content.Current.Encounters.Select(encounter => encounter.Id)));
        Assert.Equal(
            Keys(embedded.Missions.Select(mission => mission.Id)),
            Keys(content.Current.Missions.Select(mission => mission.Id)));
        Assert.Equal(
            Keys(embedded.Scenes.Select(scene => scene.Id)),
            Keys(content.Current.Scenes.Select(scene => scene.Id)));

        // The whole warehouse mission survives the round trip, not just its id.
        var mission = content.Current.FindMission(MissionKey)!;
        var authored = embedded.FindMission(MissionKey)!;
        Assert.Equal(authored.DisplayName, mission.DisplayName);
        Assert.Equal(authored.EncounterId, mission.EncounterId);
        Assert.Equal(authored.EntryLinkRoomId, mission.EntryLinkRoomId);
        Assert.Equal(authored.Rewards, mission.Rewards);
        Assert.Equal(
            authored.Objectives.Select(objective => (objective.Id, objective.Kind, objective.ItemId)),
            mission.Objectives.Select(objective => (objective.Id, objective.Kind, objective.ItemId)));
    }

    private static Guid[] Keys(IEnumerable<Guid> ids) => ids.Order().ToArray();

    [Fact]
    public async Task SavingADraft_LeavesTheRunningGameUntouched()
    {
        await using var scope = _provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var content = _provider.GetRequiredService<IGameContentProvider>();

        var saved = await store.SaveDraftAsync(
            GameContentKind.Mission, MissionKey, "Renamed Job", RenamedMissionJson("Renamed Job"), Actor);

        Assert.Equal(GameContentStatus.Published, saved.Status);
        Assert.True(saved.HasPendingEdits);

        // Even a reload — the strongest thing short of a publish — serves the
        // published payload, not the draft.
        await content.ReloadAsync();
        Assert.Equal("Gang Warehouse Retrieval", content.Current.FindMission(MissionKey)!.DisplayName);
    }

    [Fact]
    public async Task Publishing_PutsTheDraftIntoPlay()
    {
        await using var scope = _provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        var content = _provider.GetRequiredService<IGameContentProvider>();

        await store.SaveDraftAsync(
            GameContentKind.Mission, MissionKey, "Warehouse Job", RenamedMissionJson("Warehouse Job"), Actor);

        var result = await publisher.PublishAsync(GameContentKind.Mission, MissionKey, Actor);

        Assert.True(result.IsSuccess, result.Error);
        // The publisher reloads the provider itself — a publish that did not
        // reach the running game would be a publish that did nothing.
        Assert.Equal("Warehouse Job", content.Current.FindMission(MissionKey)!.DisplayName);

        var republished = await store.FindAsync(GameContentKind.Mission, MissionKey);
        Assert.False(republished!.HasPendingEdits);
        Assert.NotNull(republished.PublishedAtUtc);
    }

    [Fact]
    public async Task Publishing_RefusesADraftTheLoaderWouldReject_AndLeavesTheLiveContentAlone()
    {
        await using var scope = _provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        var content = _provider.GetRequiredService<IGameContentProvider>();

        // Names an encounter nothing declares — the same reference-integrity
        // check the embedded bundle gets at startup.
        await store.SaveDraftAsync(GameContentKind.Mission, GhostJob, "Ghost Job", GhostJobJson, Actor);

        var result = await publisher.PublishAsync(GameContentKind.Mission, GhostJob, Actor);

        Assert.False(result.IsSuccess);
        Assert.Contains("Job 'Ghost Job' names a job site that does not exist", result.Error);

        var refused = await store.FindAsync(GameContentKind.Mission, GhostJob);
        Assert.Equal(GameContentStatus.Draft, refused!.Status);
        Assert.Null(refused.PublishedJson);

        await content.ReloadAsync();
        Assert.Null(content.Current.FindMission(GhostJob));
    }

    [Fact]
    public async Task ValidateDraft_IsADryRunThatPublishesNothing()
    {
        await using var scope = _provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();

        var dryRunJob = Guid.NewGuid();
        await store.SaveDraftAsync(
            GameContentKind.Mission, dryRunJob, "Dry Run Job",
            RenamedMissionJson("Dry Run Job", dryRunJob), Actor);

        Assert.True((await publisher.ValidateDraftAsync(GameContentKind.Mission, dryRunJob)).IsSuccess);
        Assert.Equal(
            GameContentStatus.Draft,
            (await store.FindAsync(GameContentKind.Mission, dryRunJob))!.Status);
        Assert.True((await publisher.ValidatePublishedAsync()).IsSuccess);
    }

    [Fact]
    public async Task Publishing_AnUnknownDefinition_Fails()
    {
        await using var scope = _provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();

        var noSuchMission = Guid.NewGuid();
        var result = await publisher.PublishAsync(GameContentKind.Mission, noSuchMission, Actor);

        Assert.False(result.IsSuccess);
        Assert.Contains(noSuchMission.ToString(), result.Error);
    }

    private static readonly Guid GhostJob = Guid.NewGuid();

    private static readonly string GhostJobJson = $$"""
        {
          "id": "{{GhostJob}}",
          "displayName": "Ghost Job",
          "description": "A job whose site does not exist.",
          "encounterId": "{{Guid.NewGuid()}}",
          "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
          "repeatability": { "kind": "oneTime" },
          "rewards": { "karma": 1, "nuyen": 100 },
          "objectives": [
            { "id": "{{Guid.NewGuid()}}", "displayName": "Enter", "kind": "enterEncounter" }
          ]
        }
        """;

    // The warehouse mission with a new display name (and optionally a new id),
    // everything else exactly as authored.
    private static string RenamedMissionJson(string displayName, Guid? newId = null)
    {
        var authored = GameContentComposer.Split(EmbeddedGameContentProvider.ReadMergedJson())
            .Single(fragment => fragment.Kind == GameContentKind.Mission && fragment.ContentKey == MissionKey);

        var id = newId ?? MissionKey;
        var renamed = authored.Json
            .Replace($"\"id\":\"{MissionKey}\"", $"\"id\":\"{id}\"", StringComparison.Ordinal)
            .Replace(
                "\"displayName\":\"Gang Warehouse Retrieval\"",
                $"\"displayName\":\"{displayName}\"",
                StringComparison.Ordinal);
        if (id == MissionKey)
        {
            return renamed;
        }

        // WF-26: a second job on the same site cannot listen to the events
        // the first already does, so the copy listens to nothing.
        // WF-27: every record has its own id, so the copy's objectives get
        // new ones too.
        var copy = System.Text.Json.Nodes.JsonNode.Parse(renamed)!.AsObject();
        copy.Remove("triggers");
        foreach (var objective in copy["objectives"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>())
        {
            objective["id"] = Guid.NewGuid().ToString();
        }

        return copy.ToJsonString();
    }

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);
}
