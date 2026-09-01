using System.Collections.Concurrent;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Hand-rolled fakes for the action pipeline; the suite has no mocking library
// and these stay small enough not to want one.

internal sealed class FakePlaySessionStore : IPlaySessionStore
{
    private int calls;

    public ActivePlaySession? Session { get; set; }

    // Awaited (when set) before each lookup returns — lets a test hold the
    // first command mid-execution to observe queue serialization.
    public Func<int, Task>? OnGetActive { get; set; }

    public int Calls => Volatile.Read(ref calls);

    public async Task<ActivePlaySession?> GetActiveByUserIdAsync(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var call = Interlocked.Increment(ref calls);
        if (OnGetActive is not null)
        {
            await OnGetActive(call);
        }

        return Session;
    }

    public Task<StartPlaySessionResult> StartOrResumeAsync(
        Guid userId, Guid characterId, TimeSpan idleTimeout, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EndedPlaySession?> EndAsync(Guid playSessionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EndedPlaySession?> EndActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<DateTimeOffset?> RenewActivityByUserIdAsync(
        Guid userId, TimeSpan idleTimeout, TimeSpan throttleInterval, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> TryEndExpiredAsync(Guid playSessionId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeSheetLoader : IComposedSheetLoader
{
    public ComposedSheetLoadResult Result { get; set; } =
        ComposedSheetLoadResult.Failure(ComposedSheetLoadError.NotFound);

    public Task<ComposedSheetLoadResult> LoadAsync(
        Guid userId, Guid characterId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}

internal sealed class FakeRuntimeStateStore : ICharacterRuntimeStateStore
{
    public int CurrentEdge { get; set; } = 3;
    public int PhysicalDamage { get; set; }
    public int StunDamage { get; set; }

    public Task<CharacterRuntimeSnapshot> GetOrCreateAsync(
        Guid characterId, int maxEdge, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CharacterRuntimeSnapshot(characterId, PhysicalDamage, StunDamage, CurrentEdge));
}

internal sealed class FakeActiveEffectReader : IActiveEffectReader
{
    public List<ActiveEffectSnapshot> Effects { get; } = new();

    public Task<IReadOnlyList<ActiveEffectSnapshot>> GetActiveAsync(
        Guid characterId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActiveEffectSnapshot>>(Effects.ToArray());
}

internal sealed class FixedSeedSource : ISeedSource
{
    public long Seed { get; set; } = 20260830;

    public long NextSeed() => Seed;
}

// Replays a queued script of outcomes regardless of the requested pool; the
// helper keeps hits/ones/glitch consistent with the dice handed in.
internal sealed class ScriptedDiceRoller : IDiceRoller
{
    private readonly Queue<DiceRollOutcome> outcomes = new();

    public ScriptedDiceRoller Enqueue(params int[] dice)
    {
        outcomes.Enqueue(Outcome(dice));
        return this;
    }

    public DiceRollOutcome Roll(DiceRollRequest request) =>
        outcomes.Count > 0
            ? outcomes.Dequeue()
            : throw new InvalidOperationException("The dice script ran out of rolls.");

    public static DiceRollOutcome Outcome(params int[] dice)
    {
        var hits = dice.Count(die => die >= 5);
        var ones = dice.Count(die => die == 1);
        var glitch = dice.Length > 0 && ones * 2 > dice.Length;
        return new DiceRollOutcome(dice, hits, ones, glitch, glitch && hits == 0);
    }
}

internal sealed class FakeDecisionBroker : IDecisionBroker
{
    // Null answer = nobody responded: the broker times out to the default.
    public string? AnswerOptionId { get; set; }
    public PendingDecision? Captured { get; private set; }

    public Task<DecisionResolution> AwaitAsync(
        PendingDecision decision, CancellationToken cancellationToken = default)
    {
        Captured = decision;
        return Task.FromResult(AnswerOptionId is string answer
            ? new DecisionResolution(
                answer,
                WasDefault: string.Equals(answer, decision.DefaultOptionId, StringComparison.Ordinal),
                TimedOut: false)
            : new DecisionResolution(decision.DefaultOptionId, WasDefault: true, TimedOut: true));
    }

    public DecisionResponseResult TryResolve(Guid decisionId, Guid userId, string optionId) =>
        DecisionResponseResult.NotFound;
}

internal sealed class FakeStateChangeApplier : IStateChangeApplier
{
    public List<(Guid CharacterId, IReadOnlyList<StateChange> Changes)> Applications { get; } = new();

    // Lets a test script the disposition for attaches (e.g. a stacking skip).
    public Func<AttachEffectChange, AppliedStateChange>? OnAttach { get; set; }

    public IReadOnlyList<StateChange> AllChanges =>
        Applications.SelectMany(application => application.Changes).ToArray();

    public Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(
        Guid characterId, IReadOnlyList<StateChange> changes, CancellationToken cancellationToken = default)
    {
        Applications.Add((characterId, changes));

        IReadOnlyList<AppliedStateChange> applied = changes.Select(change => change switch
        {
            SpendEdgeChange spend => new AppliedStateChange("SpendEdge", $"Spent {spend.Amount} Edge ({spend.Reason})."),
            AttachEffectChange attach => OnAttach?.Invoke(attach)
                ?? new AppliedStateChange(
                    "AttachEffect", $"{attach.Effect.DisplayName} attached.", EffectAttachDisposition.Attached),
            RemoveEffectChange remove => new AppliedStateChange("RemoveEffect", $"Removed {remove.SourceId}."),
            _ => new AppliedStateChange(change.GetType().Name, "applied"),
        }).ToArray();

        return Task.FromResult(applied);
    }
}

internal sealed class FakeGameTestAuditStore : IGameTestAuditStore
{
    public ConcurrentQueue<GameTestAuditEntry> Entries { get; } = new();

    public Task AppendAsync(GameTestAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}

internal sealed class FakeRoomChatStore : IRoomChatStore
{
    public Guid RoomId { get; set; } = Guid.NewGuid();
    public ConcurrentQueue<(string Content, ChatMessageType Type)> Sent { get; } = new();

    public Task<SendRoomMessageOutcome?> SendMessageAsync(
        Guid userId, string content, ChatMessageType type, TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((content, type));
        var message = new RoomMessage(
            Guid.NewGuid(), RoomId, Guid.NewGuid(), "Case", content, type, DateTimeOffset.UtcNow);
        return Task.FromResult<SendRoomMessageOutcome?>(
            new SendRoomMessageOutcome(message, DateTimeOffset.UtcNow.AddMinutes(60)));
    }
}

internal sealed class FakeGameMessageBroadcaster : IGameMessageBroadcaster
{
    public ConcurrentQueue<RoomMessage> Broadcasts { get; } = new();
    public ConcurrentQueue<CombatView> CombatViews { get; } = new();
    public ConcurrentQueue<(Guid UserId, PendingDecisionInfo Decision)> Decisions { get; } = new();

    public Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default)
    {
        Broadcasts.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task BroadcastCombatAsync(CombatView view, CancellationToken cancellationToken = default)
    {
        CombatViews.Enqueue(view);
        return Task.CompletedTask;
    }

    public Task NotifyDecisionAsync(
        Guid userId, PendingDecisionInfo decision, CancellationToken cancellationToken = default)
    {
        Decisions.Enqueue((userId, decision));
        return Task.CompletedTask;
    }
}

// In-memory room content; also what the real AffordanceService reads in tests.
internal sealed class FakeRoomContentReader : IRoomContentReader
{
    public List<NpcSnapshot> Npcs { get; } = new();
    public List<InteractableSnapshot> Interactables { get; } = new();
    public HashSet<Guid> DiscoveredInteractables { get; } = new();
    public Dictionary<Guid, int> EnvironmentModifiers { get; } = new();

    public Task<NpcSnapshot?> GetNpcAsync(Guid npcId, CancellationToken cancellationToken) =>
        Task.FromResult(Npcs.FirstOrDefault(npc => npc.Id == npcId));

    public Task<IReadOnlyList<NpcSnapshot>> GetNpcsInRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NpcSnapshot>>(Npcs.Where(npc => npc.RoomId == roomId).ToArray());

    public Task<InteractableSnapshot?> GetInteractableAsync(Guid interactableId, CancellationToken cancellationToken) =>
        Task.FromResult(Interactables.FirstOrDefault(interactable => interactable.Id == interactableId));

    public Task<IReadOnlyList<InteractableSnapshot>> GetInteractablesInRoomAsync(
        Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InteractableSnapshot>>(
            Interactables.Where(interactable => interactable.RoomId == roomId).ToArray());

    public Task<IReadOnlySet<Guid>> GetDiscoveredSubjectIdsAsync(
        Guid characterId, DiscoverySubjectType subjectType, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(
            subjectType == DiscoverySubjectType.Interactable ? DiscoveredInteractables : new HashSet<Guid>());

    public Task<int> GetRoomEnvironmentModifierAsync(Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult(EnvironmentModifiers.TryGetValue(roomId, out var modifier) ? modifier : 0);
}

// Captures reactions the executor fires instead of running them — the tests
// assert what was enqueued, not a second resolution.
// The real embedded game content, loaded once for the whole suite — tests
// that need a mission definition use the shipped gang-warehouse content.
internal static class TestGameContent
{
    public static readonly EmbeddedGameContentProvider Provider = new();
}

// Milestone 5 fakes: mission state is empty by default (no missions, no
// encounters, no items), which keeps every pre-existing pipeline test in the
// shared world.
internal sealed class FakeMissionReader : IMissionReader
{
    public List<MissionInstanceSnapshot> Instances { get; } = [];
    public List<EncounterInstanceSnapshot> Encounters { get; } = [];
    public List<WorldItemSnapshot> Items { get; } = [];
    public Dictionary<Guid, Guid> ParticipantsByCharacter { get; } = [];

    public Task<MissionInstanceSnapshot?> GetInstanceAsync(Guid missionInstanceId, CancellationToken cancellationToken) =>
        Task.FromResult(Instances.FirstOrDefault(instance => instance.Id == missionInstanceId));

    public Task<IReadOnlyList<MissionInstanceSnapshot>> GetOpenInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MissionInstanceSnapshot>>(
            Instances.Where(instance => instance.CharacterId == characterId && !instance.IsTerminal).ToList());

    public Task<IReadOnlyList<MissionInstanceSnapshot>> ListInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MissionInstanceSnapshot>>(
            Instances.Where(instance => instance.CharacterId == characterId).ToList());

    public Task<EncounterInstanceSnapshot?> GetActiveEncounterForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken) =>
        Task.FromResult(ParticipantsByCharacter.TryGetValue(characterId, out var encounterId)
            ? Encounters.FirstOrDefault(encounter =>
                encounter.Id == encounterId && encounter.Status == EncounterInstanceStatus.Active)
            : null);

    public Task<EncounterInstanceSnapshot?> GetActiveEncounterByRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        // The fake has no room table; tests register encounters by entry room.
        Task.FromResult(Encounters.FirstOrDefault(encounter =>
            encounter.EntryRoomId == roomId && encounter.Status == EncounterInstanceStatus.Active));

    public Task<EncounterInstanceSnapshot?> GetActiveEncounterForMissionAsync(
        Guid missionInstanceId, CancellationToken cancellationToken) =>
        Task.FromResult(Encounters.FirstOrDefault(encounter =>
            encounter.MissionInstanceId == missionInstanceId
            && encounter.Status == EncounterInstanceStatus.Active));

    public Task<WorldItemSnapshot?> GetItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == itemId));

    public Task<IReadOnlyList<WorldItemSnapshot>> GetItemsInRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorldItemSnapshot>>(
            Items.Where(item => item.RoomId == roomId).ToList());
}

internal sealed class FakeTravelNotifier : ITravelNotifier
{
    public ConcurrentQueue<(Guid PlaySessionId, Guid OldRoomId, Guid NewRoomId)> Moves { get; } = new();

    public Task NotifyMovedAsync(
        Guid playSessionId, Guid oldRoomId, Guid newRoomId, CancellationToken cancellationToken = default)
    {
        Moves.Enqueue((playSessionId, oldRoomId, newRoomId));
        return Task.CompletedTask;
    }
}

// Identity scope resolution: every room is its own scope, as in the shared
// world. Instance-scope resolution is exercised by the infrastructure tests.
internal sealed class FakeGameScopeResolver : IGameScopeResolver
{
    public Task<Guid> ResolveScopeAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Task.FromResult(roomId);
}

internal sealed class FakeGameCommandQueue : IGameCommandQueue
{
    public ConcurrentQueue<(Guid ScopeId, GameActionRequest Request)> Enqueued { get; } = new();

    public Task<GameActionOutcome> EnqueueAsync(
        Guid scopeId, GameActionRequest request, CancellationToken cancellationToken = default)
    {
        Enqueued.Enqueue((scopeId, request));
        return Task.FromResult(GameActionOutcome.Final(null, null));
    }
}
