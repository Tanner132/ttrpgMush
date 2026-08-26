using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCareer;

public sealed record AttributeAdvancementEligibility(
    string AttributeId,
    int CurrentValue,
    int NewValue,
    int KarmaCost,
    int NaturalMaximum,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons);

// Shared attribute/Edge/Magic/Resonance advancement rules (SHEET-901 §§1-2):
// cost = new rating x 5, capped at each attribute's own natural maximum
// (metatype/metavariant range, +1 for Exceptional Attribute; Edge instead
// gets +1 from Lucky; Magic/Resonance use a flat 6/7 since Initiation isn't
// implemented until SHEET-909). Used both to build read-only NextActions
// (EvaluateAll) and to authoritatively validate a mutation (Evaluate) — one
// rule implementation, two callers.
public sealed class AttributeAdvancementEvaluator
{
    private const int KarmaCostPerRating = 5;
    private const int MagicResonanceNaturalMaximum = 6;
    private const int MagicResonanceExceptionalMaximum = 7;

    public IReadOnlyList<AttributeAdvancementEligibility> EvaluateAll(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma)
    {
        var ids = composedSheet.Attributes.Select(item => item.Id)
            .Concat(composedSheet.SpecialAttributes.Select(item => item.Id));

        return ids
            .Select(id => Evaluate(catalog, composedSheet, currentKarma, id))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    public AttributeAdvancementEligibility? Evaluate(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string attributeId)
    {
        var isSpecial = composedSheet.SpecialAttributes.Any(item => item.Id == attributeId);
        var attribute = isSpecial
            ? composedSheet.SpecialAttributes.FirstOrDefault(item => item.Id == attributeId)
            : composedSheet.Attributes.FirstOrDefault(item => item.Id == attributeId);
        if (attribute is null)
        {
            return null;
        }

        var currentValue = attribute.AbsoluteValue;
        var newValue = currentValue + 1;
        var karmaCost = newValue * KarmaCostPerRating;
        var hasExceptionalAttribute = HasExceptionalAttribute(composedSheet.Qualities, attributeId);
        var naturalMaximum = ResolveNaturalMaximum(catalog, composedSheet, attributeId, hasExceptionalAttribute);

        var reasons = new List<string>();
        var displayName = catalog.Attributes.TryGetValue(attributeId, out var definition) ? definition.DisplayName : attributeId;

        if (naturalMaximum is null)
        {
            reasons.Add($"{displayName} has no defined natural maximum for this metatype.");
        }
        else if (newValue > naturalMaximum)
        {
            reasons.Add($"{displayName} is already at its natural maximum of {naturalMaximum}.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new AttributeAdvancementEligibility(
            attributeId, currentValue, newValue, karmaCost, naturalMaximum ?? currentValue, reasons.Count == 0, reasons);
    }

    private static int? ResolveNaturalMaximum(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        string attributeId,
        bool hasExceptionalAttribute)
    {
        if (attributeId is "magic" or "resonance")
        {
            return hasExceptionalAttribute ? MagicResonanceExceptionalMaximum : MagicResonanceNaturalMaximum;
        }

        var range = EffectiveAttributeRange(catalog, composedSheet.Metatype, attributeId);
        if (range is null)
        {
            return null;
        }

        var maximum = range.Maximum + (hasExceptionalAttribute ? 1 : 0);
        return attributeId == "edge" && HasQuality(composedSheet.Qualities, "lucky") ? maximum + 1 : maximum;
    }

    private static MetatypeAttributeRange? EffectiveAttributeRange(
        RulesetCatalog catalog,
        CanonicalMetatype? metatype,
        string attributeId)
    {
        if (metatype is null)
        {
            return null;
        }

        if (metatype.MetavariantId is not null
            && catalog.Metavariants.TryGetValue(metatype.MetavariantId, out var metavariant)
            && metavariant.Attributes.TryGetValue(attributeId, out var metavariantRange))
        {
            return metavariantRange;
        }

        return catalog.Metatypes.TryGetValue(metatype.Id, out var definition)
            && definition.Attributes.TryGetValue(attributeId, out var range)
                ? range
                : null;
    }

    private static bool HasExceptionalAttribute(IReadOnlyList<CanonicalQuality> qualities, string attributeId) =>
        qualities.Any(item => item.Id == "exceptional-attribute"
            && string.Equals(item.Parameters?.GetValueOrDefault("attribute-id"), attributeId, StringComparison.Ordinal));

    private static bool HasQuality(IReadOnlyList<CanonicalQuality> qualities, string qualityId) =>
        qualities.Any(item => item.Id == qualityId);
}
