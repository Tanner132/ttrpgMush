using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCareer;

public enum QualityRemovalMode
{
    OnDemand,
    Approval,
    Never,
}

// One positive quality the character could buy (or buy another level of).
// HeldInstances counts instances already on the composed sheet; for a rated
// quality that is its current rating. Parameters are the choices as they
// would be stored, including the server-derived rating of a rated quality.
public sealed record QualityAcquisitionEligibility(
    string QualityId,
    int HeldInstances,
    int? MaxInstances,
    int KarmaCost,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyDictionary<string, string>? Parameters = null);

// One negative quality instance the character could buy off.
public sealed record QualityRemovalEligibility(
    string Key,
    string QualityId,
    int KarmaCost,
    QualityRemovalMode Mode,
    string? Requirement,
    bool HasPendingRequest,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyDictionary<string, string>? Parameters = null);

// Workstream 2.1 rules (SHEET_901 §§1, 7; decisions
// `career.quality-expansion-eligibility`, `career.quality-removal-approval`,
// `career.quality-ratings`). Buying a positive quality costs its listed cost
// x 2; buying off a negative one costs its recorded award x 2. A rated
// quality is held as several instances, so each level is bought or bought
// off on its own. Like the attribute and skill evaluators, one implementation
// serves both the read-only next actions and the authoritative mutation check.
public sealed class QualityAdvancementEvaluator
{
    public const int CareerCostMultiplier = 2;
    public const int MaxParameterLength = 120;

    // Creation stores a rated quality's running count on each instance as a
    // "rating" choice (frontend qualityParameters.normalizeQualityParameters).
    public const string RatingParameter = "rating";

    private static readonly IReadOnlySet<string> ExceptionalAttributeIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "magic", "resonance",
    };

    // Every career-eligible positive quality, priced and checked without
    // parameters (the player has not chosen them yet). Creation-only rows are
    // left out entirely: they can never be bought, so listing them is noise.
    public IReadOnlyList<QualityAcquisitionEligibility> EvaluateAllAcquisitions(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma) =>
        catalog.Qualities.Values
            .Where(item => item.Polarity == "positive" && item.Career?.Availability == "eligible")
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => EvaluateAcquisition(catalog, composedSheet, currentKarma, item.Id, parameters: null, checkParameters: false)!)
            .ToArray();

    public IReadOnlyList<QualityRemovalEligibility> EvaluateAllRemovals(
        RulesetCatalog catalog,
        IReadOnlyList<KeyedQuality> qualities,
        int currentKarma,
        IReadOnlySet<string> pendingKeys) =>
        qualities
            .Where(item => catalog.Qualities.TryGetValue(item.Quality.Id, out var definition) && definition.Polarity == "negative")
            .Select(item => EvaluateRemoval(catalog, qualities, currentKarma, item.Key, pendingKeys)!)
            .ToArray();

    public QualityAcquisitionEligibility? EvaluateAcquisition(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string qualityId,
        IReadOnlyDictionary<string, string>? parameters,
        bool checkParameters = true)
    {
        if (!catalog.Qualities.TryGetValue(qualityId, out var definition))
        {
            return null;
        }

        var career = definition.Career;
        var held = composedSheet.Qualities.Count(item => item.Id == qualityId);
        var karmaCost = definition.Cost * CareerCostMultiplier;
        var reasons = new List<string>();
        parameters = WithDerivedRating(definition, held, parameters);

        if (definition.Polarity != "positive")
        {
            reasons.Add($"{definition.DisplayName} is a negative quality. Negative qualities can't be taken in career play.");
        }
        else if (career?.Availability != "eligible")
        {
            reasons.Add(career?.Reason is { Length: > 0 } reason
                ? $"{definition.DisplayName} can only be taken at character creation. {reason}"
                : $"{definition.DisplayName} can only be taken at character creation.");
        }

        if (held > 0 && !definition.Repeatable)
        {
            reasons.Add($"You already have {definition.DisplayName}.");
        }
        else if (career?.MaxInstances is { } maximum && held >= maximum)
        {
            reasons.Add(maximum == 1
                ? $"You already have {definition.DisplayName}."
                : $"{definition.DisplayName} is already at its maximum of {maximum}.");
        }

        foreach (var conflict in ConflictsWith(catalog, definition, composedSheet.Qualities))
        {
            reasons.Add($"{definition.DisplayName} can't be combined with {conflict}.");
        }

        reasons.AddRange(UnmetPrerequisites(catalog, composedSheet, career?.Prerequisites ?? []));

        if (checkParameters)
        {
            reasons.AddRange(ParameterProblems(catalog, composedSheet, definition, parameters));
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new QualityAcquisitionEligibility(qualityId, held, career?.MaxInstances, karmaCost, reasons.Count == 0, reasons, parameters);
    }

    public QualityRemovalEligibility? EvaluateRemoval(
        RulesetCatalog catalog,
        IReadOnlyList<KeyedQuality> qualities,
        int currentKarma,
        string key,
        IReadOnlySet<string> pendingKeys)
    {
        var target = qualities.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (target is null || !catalog.Qualities.TryGetValue(target.Quality.Id, out var definition))
        {
            return null;
        }

        var career = definition.Career;
        var mode = career?.Removal switch
        {
            "approval" => QualityRemovalMode.Approval,
            "never" => QualityRemovalMode.Never,
            _ => QualityRemovalMode.OnDemand,
        };

        // Creation stores rating x listed cost; a malformed rating of 0 would
        // make the buy-off free, so fall back to the listed cost.
        var award = target.Quality.KarmaCost > 0 ? target.Quality.KarmaCost : definition.Cost;
        var karmaCost = award * CareerCostMultiplier;
        var hasPending = pendingKeys.Contains(key);
        var reasons = new List<string>();

        if (definition.Polarity != "negative")
        {
            reasons.Add($"{definition.DisplayName} is a positive quality and can't be bought off.");
        }
        else if (mode == QualityRemovalMode.Never)
        {
            reasons.Add(career?.Reason is { Length: > 0 } reason
                ? $"{definition.DisplayName} can't be bought off. {reason}"
                : $"{definition.DisplayName} can't be bought off.");
        }

        if (hasPending)
        {
            reasons.Add($"A request to remove {definition.DisplayName} is already waiting for a Storyteller.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new QualityRemovalEligibility(
            key,
            definition.Id,
            karmaCost,
            mode,
            mode == QualityRemovalMode.Approval ? career?.RemovalRequirement : null,
            hasPending,
            reasons.Count == 0,
            reasons,
            target.Quality.Parameters);
    }

    // The server, not the client, decides a rated quality's rating: it is the
    // number of instances once this one is added.
    private static IReadOnlyDictionary<string, string>? WithDerivedRating(
        QualityDefinition definition, int held, IReadOnlyDictionary<string, string>? parameters)
    {
        if (!definition.Parameterized || definition.Career?.MaxInstances is not > 1)
        {
            return parameters;
        }

        var result = parameters is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        result[RatingParameter] = (held + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return result;
    }

    private static IEnumerable<string> ConflictsWith(
        RulesetCatalog catalog,
        QualityDefinition definition,
        IReadOnlyList<CanonicalQuality> held)
    {
        var heldIds = held.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var conflicting = definition.Conflicts.Where(heldIds.Contains)
            .Concat(heldIds.Where(id => catalog.Qualities.TryGetValue(id, out var other) && other.Conflicts.Contains(definition.Id)))
            .Distinct(StringComparer.Ordinal);
        foreach (var id in conflicting)
        {
            yield return catalog.Qualities.TryGetValue(id, out var other) ? other.DisplayName : id;
        }
    }

    private static IEnumerable<string> UnmetPrerequisites(
        RulesetCatalog catalog,
        CanonicalCharacterSheet sheet,
        IReadOnlyList<string> prerequisites)
    {
        var magic = SpecialAttribute(sheet, "magic");
        var resonance = SpecialAttribute(sheet, "resonance");
        var pathId = sheet.MagicResonance?.PathId;

        // Metatype prerequisites are alternatives ("elf, dwarf, or ork"), so
        // they are checked together; every other prerequisite must hold.
        var metatypes = prerequisites
            .Where(item => item.StartsWith(CareerQualityPrerequisites.MetatypePrefix, StringComparison.Ordinal))
            .Select(item => item[CareerQualityPrerequisites.MetatypePrefix.Length..])
            .ToArray();
        if (metatypes.Length > 0 && (sheet.Metatype is null || !metatypes.Contains(sheet.Metatype.Id, StringComparer.Ordinal)))
        {
            var names = metatypes.Select(id => catalog.Metatypes.TryGetValue(id, out var metatype) ? metatype.DisplayName : id);
            yield return $"Only available to these metatypes: {string.Join(", ", names)}.";
        }

        foreach (var prerequisite in prerequisites)
        {
            if (prerequisite.StartsWith(CareerQualityPrerequisites.QualityPrefix, StringComparison.Ordinal))
            {
                var requiredId = prerequisite[CareerQualityPrerequisites.QualityPrefix.Length..];
                if (!sheet.Qualities.Any(item => item.Id == requiredId))
                {
                    var name = catalog.Qualities.TryGetValue(requiredId, out var required) ? required.DisplayName : requiredId;
                    yield return $"Requires the {name} quality.";
                }

                continue;
            }

            var problem = prerequisite switch
            {
                CareerQualityPrerequisites.Magic when magic <= 0 => "Requires a Magic rating.",
                CareerQualityPrerequisites.NoMagic when magic > 0 => "Only available to characters with no Magic rating.",
                CareerQualityPrerequisites.Resonance when resonance <= 0 => "Requires a Resonance rating.",
                CareerQualityPrerequisites.MagicOrResonance when magic <= 0 && resonance <= 0 => "Requires a Magic or Resonance rating.",
                CareerQualityPrerequisites.Mundane when magic > 0 || resonance > 0 => "Only available to mundane characters.",
                CareerQualityPrerequisites.Spellcaster when !IsSpellcaster(sheet) => "Requires a spellcasting path.",
                CareerQualityPrerequisites.Adept when pathId is not ("adept" or "mystic-adept") => "Requires the adept or mystic adept path.",
                // Submersion arrives with workstream 2.2; until then no
                // character has a grade, so this always blocks.
                CareerQualityPrerequisites.Submersion => "Requires at least one grade of Submersion.",
                _ => null,
            };
            if (problem is not null)
            {
                yield return problem;
            }
        }
    }

    private static bool IsSpellcaster(CanonicalCharacterSheet sheet) =>
        sheet.MagicResonance?.PathId switch
        {
            "magician" or "mystic-adept" => true,
            "aspected-magician" => sheet.MagicResonance.AspectedValueId == "sorcery",
            _ => false,
        };

    private static IEnumerable<string> ParameterProblems(
        RulesetCatalog catalog,
        CanonicalCharacterSheet sheet,
        QualityDefinition definition,
        IReadOnlyDictionary<string, string>? parameters)
    {
        var supplied = parameters?.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToArray() ?? [];
        if (!definition.Parameterized)
        {
            if (parameters is { Count: > 0 })
            {
                yield return $"{definition.DisplayName} takes no choices.";
            }

            yield break;
        }

        if (supplied.Length == 0 || parameters!.Values.Any(string.IsNullOrWhiteSpace))
        {
            yield return $"Complete every choice {definition.DisplayName} needs.";
            yield break;
        }

        if (parameters.Keys.Any(string.IsNullOrWhiteSpace) || parameters.Keys.Any(key => key.Length > MaxParameterLength))
        {
            yield return "A choice has an invalid name.";
        }

        if (parameters.Values.Any(value => value.Length > MaxParameterLength))
        {
            yield return $"Use plain text of {MaxParameterLength} characters or fewer.";
        }

        // The choices rules code reads (SHEET_901 §7: "Aptitude's skill-id
        // must reference a skill the character currently has").
        switch (definition.Id)
        {
            case "aptitude":
                var skillId = parameters.GetValueOrDefault("skill-id");
                if (skillId is null || !sheet.Skills.Any(item => item.Id == skillId && item.TotalRating > 0))
                {
                    yield return "Choose an active skill you already have a rating in.";
                }

                break;
            case "exceptional-attribute":
                var attributeId = parameters.GetValueOrDefault("attribute-id");
                if (attributeId is null || !ExceptionalAttributeIds.Contains(attributeId))
                {
                    yield return "Choose Body, Agility, Reaction, Strength, Willpower, Logic, Intuition, Charisma, Magic, or Resonance.";
                }
                else if (attributeId is "magic" or "resonance" && SpecialAttribute(sheet, attributeId) <= 0)
                {
                    yield return "You can only choose Magic or Resonance if you have a rating in it.";
                }

                break;
            case "mentor-spirit":
                var mentorId = parameters.GetValueOrDefault("mentor-id");
                if (mentorId is null || !catalog.MentorSpirits.ContainsKey(mentorId))
                {
                    yield return "Choose a mentor spirit.";
                }

                break;
        }
    }

    private static int SpecialAttribute(CanonicalCharacterSheet sheet, string id) =>
        sheet.SpecialAttributes.FirstOrDefault(item => item.Id == id)?.AbsoluteValue ?? 0;
}
