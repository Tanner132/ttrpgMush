using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Actions;

// The action pipeline (§14/§47): validate → resolve (pausing on decisions) →
// apply State Changes atomically → audit → notify. Runs on the queue's
// consumer, one action at a time per room scope; the queue blocks while a
// resolution awaits a decision (MVP pause rule), so a Pending result is
// always finalized before the next action of that scope begins.
public sealed class GameActionExecutor
{
    private const string OptionYes = "yes";
    private const string OptionNo = "no";
    private static readonly TimeSpan SecondChanceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SurgeLifetime = TimeSpan.FromSeconds(60);

    // Persistence JSON convention: camelCase properties, enum names as
    // strings (matches the career document serialization style).
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IPlaySessionStore playSessionStore;
    private readonly IComposedSheetLoader sheetLoader;
    private readonly ICharacterRuntimeStateStore runtimeStateStore;
    private readonly IActiveEffectReader effectReader;
    private readonly ISeedSource seedSource;
    private readonly TestResolver resolver;
    private readonly IDiceRoller roller;
    private readonly IDecisionBroker decisionBroker;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IRoomChatStore chatStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly PlaySessionOptions playSessionOptions;
    private readonly TimeProvider timeProvider;

    public GameActionExecutor(
        IPlaySessionStore playSessionStore,
        IComposedSheetLoader sheetLoader,
        ICharacterRuntimeStateStore runtimeStateStore,
        IActiveEffectReader effectReader,
        ISeedSource seedSource,
        TestResolver resolver,
        IDiceRoller roller,
        IDecisionBroker decisionBroker,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IRoomChatStore chatStore,
        IGameMessageBroadcaster broadcaster,
        PlaySessionOptions playSessionOptions,
        TimeProvider timeProvider)
    {
        this.playSessionStore = playSessionStore;
        this.sheetLoader = sheetLoader;
        this.runtimeStateStore = runtimeStateStore;
        this.effectReader = effectReader;
        this.seedSource = seedSource;
        this.resolver = resolver;
        this.roller = roller;
        this.decisionBroker = decisionBroker;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.chatStore = chatStore;
        this.broadcaster = broadcaster;
        this.playSessionOptions = playSessionOptions;
        this.timeProvider = timeProvider;
    }

    // Runs the action to completion and returns the final outcome. When the
    // resolution pauses on a decision, `publishInitialOutcome` delivers the
    // AwaitingDecision outcome to the submitting caller while this method
    // keeps running until the decision (or its timeout default) resolves.
    public async Task<GameActionOutcome> ExecuteAsync(
        GameActionRequest request,
        Action<GameActionOutcome>? publishInitialOutcome = null,
        CancellationToken cancellationToken = default)
    {
        var definition = DevelopmentGameActions.Find(request.ActionId);
        if (definition is null)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotFound);
        }

        var session = await playSessionStore.GetActiveByUserIdAsync(
            request.UserId, timeProvider.GetUtcNow(), cancellationToken);
        if (session is null)
        {
            return GameActionOutcome.Failure(GameActionError.NoActiveSession);
        }

        var sheetResult = await sheetLoader.LoadAsync(request.UserId, session.CharacterId, cancellationToken);
        if (!sheetResult.IsSuccess || sheetResult.Adapter is null)
        {
            return GameActionOutcome.Failure(GameActionError.CharacterSheetUnavailable);
        }

        var runtime = await runtimeStateStore.GetOrCreateAsync(
            session.CharacterId, sheetResult.Adapter.GetMaxEdge(), cancellationToken);

        var effects = await effectReader.GetActiveAsync(
            session.CharacterId, timeProvider.GetUtcNow(), cancellationToken);

        return definition.Kind == GameActionKind.Test
            ? await ExecuteTestAsync(
                request, definition, session, sheetResult.Adapter, runtime, effects,
                publishInitialOutcome, cancellationToken)
            : await ExecuteUtilityAsync(request, definition, session, effects, cancellationToken);
    }

    private async Task<GameActionOutcome> ExecuteTestAsync(
        GameActionRequest request,
        GameActionDefinition definition,
        ActivePlaySession session,
        CharacterRulesAdapter adapter,
        CharacterRuntimeSnapshot runtime,
        IReadOnlyList<ActiveEffectSnapshot> effects,
        Action<GameActionOutcome>? publishInitialOutcome,
        CancellationToken cancellationToken)
    {
        if (request.PushTheLimit && runtime.CurrentEdge < 1)
        {
            return GameActionOutcome.Failure(GameActionError.NotEnoughEdge);
        }

        var built = SkillTestBuilder.Build(
            definition.Test!, adapter, runtime, request.SituationalModifier ?? 0, effects);

        var modifiers = built.Modifiers;
        var rollOptions = RollOptions.Default;
        var edgeSpent = 0;

        if (request.PushTheLimit)
        {
            // §20 pre-roll: + Edge rating dice, Rule of Six, no limit.
            modifiers = modifiers
                .Append(new Modifier(
                    "Edge — Push the Limit",
                    ModifierTarget.DicePool,
                    ModifierOperation.Add,
                    adapter.GetMaxEdge()))
                .ToArray();
            rollOptions = new RollOptions(ExplodingSixes: true, IgnoreLimit: true);
            edgeSpent = 1;
        }

        var seed = seedSource.NextSeed();
        var resolution = resolver.Resolve(built.Spec, modifiers, seed, rollOptions);
        if (request.PushTheLimit)
        {
            resolution = resolution with { Edge = EdgeAction.PushTheLimit };
        }

        var decisions = new List<DecisionAudit>();

        if (EdgeRules.CanOfferSecondChance(resolution, runtime.CurrentEdge))
        {
            resolution = resolution with { Status = ResolutionStatus.Pending };

            var nonHits = resolution.Dice.Count(die => die < 5);
            var pending = new PendingDecision(
                Guid.NewGuid(),
                request.UserId,
                DecisionKind.EdgeSecondChance,
                $"Spend Edge — Second Chance? Reroll {nonHits} non-hit "
                    + $"{(nonHits == 1 ? "die" : "dice")} for 1 Edge.",
                new[] { new DecisionOption(OptionYes, "Spend 1 Edge"), new DecisionOption(OptionNo, "Keep the roll") },
                DefaultOptionId: OptionNo,
                SecondChanceTimeout);

            publishInitialOutcome?.Invoke(GameActionOutcome.AwaitingDecision(
                resolution,
                new PendingDecisionInfo(
                    pending.DecisionId,
                    pending.Kind,
                    pending.Prompt,
                    pending.Options,
                    pending.DefaultOptionId,
                    (int)pending.Timeout.TotalSeconds)));

            var answer = await decisionBroker.AwaitAsync(pending, cancellationToken);
            decisions.Add(new DecisionAudit(
                pending.Kind, pending.Prompt, pending.DefaultOptionId,
                answer.OptionId, answer.WasDefault, answer.TimedOut));

            if (string.Equals(answer.OptionId, OptionYes, StringComparison.Ordinal))
            {
                resolution = EdgeRules.ApplySecondChance(resolution, roller);
                edgeSpent += 1;
            }
            else
            {
                resolution = resolution with { Status = ResolutionStatus.Final };
            }
        }

        var changes = new List<StateChange>();
        if (edgeSpent > 0)
        {
            changes.Add(new SpendEdgeChange(
                edgeSpent,
                resolution.Edge == EdgeAction.PushTheLimit ? "Push the Limit" : "Second Chance"));
        }

        var applied = changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        await AppendAuditAsync(request, session, seed, resolution, decisions, applied, cancellationToken);

        await BroadcastRollAsync(
            request.UserId,
            ResolutionFormatter.Format(session.CharacterName, resolution),
            cancellationToken);

        return GameActionOutcome.Final(resolution);
    }

    private async Task<GameActionOutcome> ExecuteUtilityAsync(
        GameActionRequest request,
        GameActionDefinition definition,
        ActivePlaySession session,
        IReadOnlyList<ActiveEffectSnapshot> effects,
        CancellationToken cancellationToken)
    {
        var (changes, emote, message) = BuildUtilityChanges(request.ActionId, session.CharacterId, effects);

        var applied = changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        // An attach the stacking rules refused turns the action into a no-op
        // report instead of a room-visible act.
        var skipped = applied.FirstOrDefault(change => change.Disposition == EffectAttachDisposition.Skipped);
        if (skipped is not null)
        {
            emote = null;
            message = skipped.Description;
        }

        await AppendAuditAsync(
            request, session, seed: 0, resolution: null,
            Array.Empty<DecisionAudit>(), applied, cancellationToken);

        if (emote is not null)
        {
            await BroadcastMessageAsync(request.UserId, emote, ChatMessageType.Emote, cancellationToken);
        }

        return GameActionOutcome.Final(null, message ?? $"{definition.DisplayName} resolved.");
    }

    private (IReadOnlyList<StateChange> Changes, string? Emote, string? Message) BuildUtilityChanges(
        string actionId,
        Guid characterId,
        IReadOnlyList<ActiveEffectSnapshot> effects)
    {
        switch (actionId)
        {
            case DevelopmentGameActions.RunActionId:
            {
                var running = effects.Any(effect =>
                    effect.SourceType == EffectSourceType.Action
                    && string.Equals(effect.SourceId, DevelopmentGameActions.RunActionId, StringComparison.Ordinal));

                if (running)
                {
                    return (
                        new StateChange[] { new RemoveEffectChange(EffectSourceType.Action, DevelopmentGameActions.RunActionId) },
                        "stops running.",
                        "You stop running.");
                }

                return (
                    new StateChange[]
                    {
                        new AttachEffectChange(new NewActiveEffect(
                            characterId,
                            EffectSourceType.Action,
                            DevelopmentGameActions.RunActionId,
                            "Running",
                            new StatusPayload(StatusKind.Running),
                            ActiveEffectDurationType.UntilRemoved,
                            Lifetime: null,
                            EffectStackingRule.Unique,
                            StackingGroup: "movement-mode")),
                    },
                    "starts running.",
                    "You start running (−2 dice on Physical tests until you stop).");
            }

            case DevelopmentGameActions.SurgeActionId:
                return (
                    new StateChange[]
                    {
                        new AttachEffectChange(new NewActiveEffect(
                            characterId,
                            EffectSourceType.Action,
                            DevelopmentGameActions.SurgeActionId,
                            "Adrenaline Surge (dev)",
                            new AttributeModifierPayload("agility", 2),
                            ActiveEffectDurationType.Timed,
                            SurgeLifetime,
                            EffectStackingRule.HighestOnly,
                            StackingGroup: "attribute-boost:agility")),
                    },
                    "tenses as adrenaline hits.",
                    "Adrenaline Surge active: Agility +2 for 60 seconds.");

            default:
                throw new NotSupportedException($"Utility action '{actionId}' has no handler.");
        }
    }

    private async Task AppendAuditAsync(
        GameActionRequest request,
        ActivePlaySession session,
        long seed,
        ResolutionResult? resolution,
        IReadOnlyList<DecisionAudit> decisions,
        IReadOnlyList<AppliedStateChange> stateChanges,
        CancellationToken cancellationToken)
    {
        var envelope = new AuditEnvelope(
            request.RequestId, request.ActionId, request.PushTheLimit, resolution, decisions, stateChanges);

        await auditStore.AppendAsync(
            new GameTestAuditEntry(
                request.UserId,
                session.CharacterId,
                session.CurrentRoomId,
                request.ActionId,
                seed,
                resolution?.Success ?? true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    private Task BroadcastRollAsync(Guid userId, string content, CancellationToken cancellationToken) =>
        BroadcastMessageAsync(userId, content, ChatMessageType.Roll, cancellationToken);

    private async Task BroadcastMessageAsync(
        Guid userId,
        string content,
        ChatMessageType type,
        CancellationToken cancellationToken)
    {
        var outcome = await chatStore.SendMessageAsync(
            userId, content, type, playSessionOptions.IdleTimeout, cancellationToken);

        if (outcome is not null)
        {
            await broadcaster.BroadcastAsync(outcome.Message, cancellationToken);
        }
    }

    // Recorded in the audit envelope: what was asked, what was chosen, and
    // whether the default answered (explicitly or by timeout).
    private sealed record DecisionAudit(
        DecisionKind Kind,
        string Prompt,
        string DefaultOptionId,
        string ChosenOptionId,
        bool WasDefault,
        bool TimedOut);

    private sealed record AuditEnvelope(
        Guid RequestId,
        string ActionId,
        bool PushTheLimit,
        ResolutionResult? Resolution,
        IReadOnlyList<DecisionAudit> Decisions,
        IReadOnlyList<AppliedStateChange> StateChanges);
}
