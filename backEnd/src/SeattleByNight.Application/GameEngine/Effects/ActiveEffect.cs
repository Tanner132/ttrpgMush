using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Effects;

// Terminology discipline (§9): an Active Effect is an ongoing condition that
// persists and influences later resolutions. A one-shot mutation is a State
// Change, never an "effect".

public enum EffectSourceType
{
    Action,
    Spell,
    Drug,
    Quality,
    Gear,
    Environment,
    Injury,
    Development,
}

// §11. Only Permanent, UntilRemoved, and Timed are evaluatable before
// structured time exists (Milestone 4); the turn/round-scoped members are
// declared now so payload schemas don't churn when combat arrives.
public enum ActiveEffectDurationType
{
    Permanent,
    UntilRemoved,
    Timed,
    UntilEndOfTurn,
    UntilStartOfNextTurn,
    UntilEndOfRound,
    Sustained,
}

// §12 stacking. Rivals are the effects sharing the incoming effect's stacking
// group (or, with no group, the same source type + id).
public enum EffectStackingRule
{
    // Coexists with everything; no rivals considered.
    Stack,
    // Attaches only when no rival is active.
    Unique,
    // Replaces weaker/equal rivals (equal magnitude refreshes duration);
    // skipped when a strictly stronger rival is active.
    HighestOnly,
    // Replaces any existing effect from the same source type + id.
    ReplaceSameSource,
}

public enum StatusKind
{
    Running,
    Prone,
}

// §10 payloads: what an effect does, serialized to jsonb with a type
// discriminator so new payload shapes never need schema migrations.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StatusPayload), "status")]
[JsonDerivedType(typeof(AttributeModifierPayload), "attributeModifier")]
[JsonDerivedType(typeof(DicePoolModifierPayload), "dicePoolModifier")]
public abstract record EffectPayload;

public sealed record StatusPayload(StatusKind Status) : EffectPayload;

public sealed record AttributeModifierPayload(string AttributeId, int Amount) : EffectPayload;

public sealed record DicePoolModifierPayload(int Amount, IReadOnlyList<TestTag> AppliesToTags) : EffectPayload;

public static class EffectPayloadJson
{
    // Persistence JSON convention: camelCase properties, enum names as
    // strings (matches the career document serialization style).
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed record ActiveEffectSnapshot(
    Guid Id,
    Guid CharacterId,
    EffectSourceType SourceType,
    string SourceId,
    string DisplayName,
    EffectPayload Payload,
    ActiveEffectDurationType Duration,
    DateTimeOffset? ExpiresAtUtc,
    EffectStackingRule Stacking,
    string? StackingGroup);

public sealed record NewActiveEffect(
    Guid CharacterId,
    EffectSourceType SourceType,
    string SourceId,
    string DisplayName,
    EffectPayload Payload,
    ActiveEffectDurationType Duration,
    TimeSpan? Lifetime,
    EffectStackingRule Stacking,
    string? StackingGroup);

// Read side only: every mutation (attach/remove) travels through
// IStateChangeApplier so it commits atomically with its sibling changes (§47).
public interface IActiveEffectReader
{
    // Returns effects active at `now`; expired Timed effects are pruned
    // lazily, so callers never see one.
    Task<IReadOnlyList<ActiveEffectSnapshot>> GetActiveAsync(
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
