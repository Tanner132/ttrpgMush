using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// World Forge phase 5: character facts against a real database — set and
// cleared in the same commit as the action, a run-only fact that stops
// counting when its run ends, and jobs that require, refuse and set facts.
public sealed class CharacterFactTests : IAsyncLifetime
{
    private static readonly Guid HelpedTheAncients = Guid.Parse("f0000000-0000-4000-8000-000000000001");
    private static readonly Guid OnTheClock = Guid.Parse("f0000000-0000-4000-8000-000000000002");
    private static readonly Guid BurnedKane = Guid.Parse("f0000000-0000-4000-8000-000000000003");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TimeProvider _clock = TimeProvider.System;
    private string _connectionString = null!;
    private Content _content = null!;
    private MissionDefinition _job = null!;

    private sealed class Content(GameContentDocument document) : IGameContentProvider
    {
        public GameContentDocument Current { get; set; } = document;

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        var shipped = new EmbeddedGameContentProvider().Current;
        Use(shipped.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!);

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
        await new JobItemStore(db, _clock).SyncAsync(_content.Current);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // The shipped job, with the facts this test gives it.
    private void Use(MissionDefinition job)
    {
        _job = job;
        var shipped = new EmbeddedGameContentProvider().Current;
        var document = shipped with
        {
            Missions = shipped.Missions.Select(mission => mission.Id == job.Id ? job : mission).ToArray(),
            Facts =
            [
                new FactDefinition(HelpedTheAncients, "Helped the Ancients", "Did the Ancients a favour."),
                new FactDefinition(OnTheClock, "Seen at the warehouse", "Only for this run.", RunOf: job.Id),
                new FactDefinition(BurnedKane, "Burned Kane", "Kane will not work with them."),
            ],
        };
        if (_content is null)
        {
            _content = new Content(document);
        }
        else
        {
            _content.Current = document;
        }
    }

    [Fact]
    public async Task A_fact_is_set_and_cleared_on_the_acting_character_only()
    {
        var runner = await CreateRunnerAsync();
        var other = await CreateRunnerAsync();

        await ApplyAsync(runner, new SetFactChange(HelpedTheAncients));
        Assert.Equal([HelpedTheAncients], await FactsAsync(runner));
        Assert.Empty(await FactsAsync(other));

        // Setting it again is not a second row.
        await ApplyAsync(runner, new SetFactChange(HelpedTheAncients));
        await ApplyAsync(runner, new ClearFactChange(HelpedTheAncients));
        Assert.Empty(await FactsAsync(runner));
    }

    [Fact]
    public async Task A_run_only_fact_counts_while_the_run_is_open_and_not_after()
    {
        var runner = await CreateRunnerAsync();

        // Not on the job: nothing to tie it to, so it is not set.
        var skipped = await ApplyAsync(runner, new SetFactChange(OnTheClock));
        Assert.Contains("not set", skipped.Single().Description);
        Assert.Empty(await FactsAsync(runner));

        await ApplyAsync(runner, new AcceptMissionChange(_job.Id, null));
        await ApplyAsync(runner, new SetFactChange(OnTheClock));
        Assert.Equal([OnTheClock], await FactsAsync(runner));

        await ApplyAsync(runner, new FailMissionChange(await InstanceIdAsync(runner)));
        Assert.Empty(await FactsAsync(runner));
    }

    [Fact]
    public async Task A_job_that_requires_a_fact_is_offered_only_to_runners_who_have_it()
    {
        Use(_job with { RequiresFacts = [HelpedTheAncients] });
        var runner = await CreateRunnerAsync();

        await using (var db = CreateDbContext())
        {
            var store = new MissionStore(db, _clock);
            Assert.False(await store.IsMissionAvailableAsync(runner, _job, CancellationToken.None));
            Assert.Equal(MissionAssignError.FactsNotMet, (await store.AssignAsync(runner, _job, CancellationToken.None)).Error);
        }

        await ApplyAsync(runner, new SetFactChange(HelpedTheAncients));

        await using (var db = CreateDbContext())
        {
            Assert.True(await new MissionStore(db, _clock).IsMissionAvailableAsync(runner, _job, CancellationToken.None));
        }
    }

    [Fact]
    public async Task A_job_that_refuses_a_fact_is_not_offered_to_runners_who_have_it()
    {
        Use(_job with { RefusesFacts = [BurnedKane] });
        var runner = await CreateRunnerAsync();
        await ApplyAsync(runner, new SetFactChange(BurnedKane));

        await using var db = CreateDbContext();
        Assert.False(await new MissionStore(db, _clock).IsMissionAvailableAsync(runner, _job, CancellationToken.None));
    }

    [Fact]
    public async Task Finishing_a_job_sets_and_clears_its_facts_in_the_same_commit_as_the_pay()
    {
        Use(_job with { CompletionSetsFacts = [HelpedTheAncients], CompletionClearsFacts = [BurnedKane] });
        var runner = await CreateRunnerAsync(withCareerState: true);
        await ApplyAsync(runner, new SetFactChange(BurnedKane));
        await ApplyAsync(runner, new AcceptMissionChange(_job.Id, null));

        await ApplyAsync(runner, new CompleteMissionChange(await InstanceIdAsync(runner), 2, 2000));

        Assert.Equal([HelpedTheAncients], await FactsAsync(runner));
    }

    // ------------------------------------------------------------- helpers

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private async Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(Guid characterId, StateChange change)
    {
        await using var db = CreateDbContext();
        var applier = new StateChangeApplier(db, _content, new MissionStore(db, _clock), _clock);
        return await applier.ApplyAsync(characterId, [change]);
    }

    private async Task<Guid[]> FactsAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return [.. (await new CharacterFactReader(db).GetFactsAsync(characterId)).Order()];
    }

    private async Task<Guid> InstanceIdAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return await db.MissionInstances.Where(row => row.CharacterId == characterId).Select(row => row.Id).SingleAsync();
    }

    private async Task<Guid> CreateRunnerAsync(bool withCareerState = false)
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            NormalizedUserName = $"USER-{userId:N}",
            Email = $"{userId:N}@test.local",
            NormalizedEmail = $"{userId:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });

        var characterId = Guid.NewGuid();
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = DevelopmentDataSeeder.AlleyId,
        });

        if (withCareerState)
        {
            db.CharacterCareerStates.Add(new CharacterCareerState
            {
                CharacterId = characterId,
                CareerDocumentSchemaVersion = 1,
                ProgressionJson = "{}",
                CreatedAtUtc = _clock.GetUtcNow(),
                UpdatedAtUtc = _clock.GetUtcNow(),
            });
        }

        await db.SaveChangesAsync();
        return characterId;
    }
}
