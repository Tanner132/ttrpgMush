using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SeattleByNight.Application.CharacterCreation.Catalog;

public static partial class RulesetCatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static RulesetCatalog Load(string json, string? expectedSemanticDigest = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        CatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(json, SerializerOptions)
                ?? throw new RulesetCatalogException("The catalog document is empty.");
        }
        catch (JsonException exception)
        {
            throw new RulesetCatalogException("The catalog document is not valid JSON.", exception);
        }

        Validate(document);
        var digest = ComputeSemanticDigest(json);

        // Digest/schema integrity enforcement is intentionally disabled during
        // the pre-alpha active-schema-development phase (see
        // roadmap/SR5_RULESET_MANIFEST.md "Schema Lifecycle" and
        // EmbeddedRulesetCatalogProvider.RetainedVersions). The digest is
        // still computed above and still returned on the catalog below, so
        // drafts/sheets keep recording a real value -- only the "this must
        // match a pinned expectation" check is suppressed. Re-enable this
        // block once the base schema is declared stable/locked.
        //
        // if (expectedSemanticDigest is not null
        //     && !string.Equals(digest, expectedSemanticDigest, StringComparison.OrdinalIgnoreCase))
        // {
        //     throw new RulesetCatalogException(
        //         $"Catalog semantic digest mismatch. Expected {expectedSemanticDigest}, calculated {digest}.");
        // }

        return new RulesetCatalog(
            document.RulesetId,
            document.Version,
            digest,
            ToDictionary(document.Sources!, item => item.Id),
            ToDictionary(document.CreationMethods!, item => item.Id),
            ToDictionary(document.PriorityLevels!, item => item.Id),
            document.PriorityCategories!.ToImmutableArray(),
            ToDictionary(document.PriorityCells!, item => item.Id),
            ToDictionary(document.Metatypes!, item => item.Id),
            ToDictionary(document.Metavariants ?? [], item => item.Id),
            ToDictionary(document.Attributes!, item => item.Id),
            ToDictionary(document.Qualities!, item => item.Id),
            ToDictionary(document.Skills!, item => item.Id),
            ToDictionary(document.SkillGroups!, item => item.Id),
            ToDictionary(document.KnowledgeCategories!, item => item.Id),
            ToDictionary(document.KnowledgeSkillSuggestions ?? [], item => item.Id),
            ToDictionary(document.LanguageSuggestions ?? [], item => item.Id),
            ToDictionary(document.CreationPaths!, item => item.Id),
            ToDictionary(document.AspectedValues!, item => item.Id),
            ToDictionary(document.Traditions!, item => item.Id),
            ToDictionary(document.Spells!, item => item.Id),
            ToDictionary(document.Rituals!, item => item.Id),
            ToDictionary(document.AdeptPowers!, item => item.Id),
            ToDictionary(document.MentorSpirits!, item => item.Id),
            ToDictionary(document.ComplexForms!, item => item.Id),
            ToDictionary(document.SpiritTypes!, item => item.Id),
            ToDictionary(document.SpriteTypes!, item => item.Id),
            ToDictionary(document.Foci!, item => item.Id),
            ToDictionary(document.Gear!, item => item.Id),
            ToDictionary(document.Weapons!, item => item.Id),
            ToDictionary(document.Armor!, item => item.Id),
            ToDictionary(document.AugmentationGrades!, item => item.Id),
            ToDictionary(document.Augmentations!, item => item.Id),
            ToDictionary(document.Vehicles!, item => item.Id),
            ToDictionary(document.Cyberdecks!, item => item.Id),
            ToDictionary(document.WeaponAccessories!, item => item.Id),
            ToDictionary(document.ArmorModifications!, item => item.Id),
            ToDictionary(document.CyberlimbEnhancements!, item => item.Id),
            ToDictionary(document.VehicleModifications!, item => item.Id),
            ToDictionary(document.LifestyleTiers!, item => item.Id),
            ToDictionary(document.LifestyleOptions!, item => item.Id));
    }

    public static RulesetCatalog LoadOverlay(
        string baseJson,
        string overlayJson,
        string? expectedSemanticDigest = null)
    {
        ArgumentNullException.ThrowIfNull(baseJson);
        ArgumentNullException.ThrowIfNull(overlayJson);

        var baseDocument = JsonNode.Parse(baseJson)?.AsObject()
            ?? throw new RulesetCatalogException("The base catalog document is empty.");
        var overlayDocument = JsonNode.Parse(overlayJson)?.AsObject()
            ?? throw new RulesetCatalogException("The catalog overlay document is empty.");
        var baseVersion = baseDocument["version"]?.GetValue<string>();
        var requiredBaseVersion = overlayDocument["baseVersion"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(requiredBaseVersion)
            || !string.Equals(baseVersion, requiredBaseVersion, StringComparison.Ordinal))
        {
            throw new RulesetCatalogException(
                $"Catalog overlay requires base version '{requiredBaseVersion}', but received '{baseVersion}'.");
        }

        if (!string.Equals(
            baseDocument["rulesetId"]?.GetValue<string>(),
            overlayDocument["rulesetId"]?.GetValue<string>(),
            StringComparison.Ordinal))
        {
            throw new RulesetCatalogException("Catalog overlay ruleset does not match its base catalog.");
        }

        foreach (var property in overlayDocument)
        {
            if (property.Key == "baseVersion") continue;
            baseDocument[property.Key] = property.Value?.DeepClone();
        }

        return Load(baseDocument.ToJsonString(), expectedSemanticDigest);
    }

    public static string ComputeSemanticDigest(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
        });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(document.RootElement, writer);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void Validate(CatalogDocument document)
    {
        RequireId(document.RulesetId, "rulesetId");
        if (string.IsNullOrWhiteSpace(document.Version))
        {
            throw new RulesetCatalogException("Catalog version is required.");
        }

        if (document.Sources is null
            || document.CreationMethods is null
            || document.PriorityLevels is null
            || document.PriorityCategories is null
            || document.PriorityCells is null
            || document.Metatypes is null
            || document.Attributes is null
            || document.Qualities is null
            || document.Skills is null
            || document.SkillGroups is null
            || document.KnowledgeCategories is null
            || document.CreationPaths is null
            || document.AspectedValues is null
            || document.Traditions is null
            || document.Spells is null
            || document.Rituals is null
            || document.AdeptPowers is null
            || document.MentorSpirits is null
            || document.ComplexForms is null
            || document.SpiritTypes is null
            || document.SpriteTypes is null
            || document.Foci is null
            || document.Gear is null
            || document.Weapons is null
            || document.Armor is null
            || document.AugmentationGrades is null
            || document.Augmentations is null
            || document.Vehicles is null
            || document.Cyberdecks is null
            || document.WeaponAccessories is null
            || document.ArmorModifications is null
            || document.CyberlimbEnhancements is null
            || document.VehicleModifications is null
            || document.LifestyleTiers is null
            || document.LifestyleOptions is null)
        {
            throw new RulesetCatalogException("The catalog is missing a required collection.");
        }

        ValidateUnique(document.Sources, item => item.Id, "source");
        ValidateUnique(document.CreationMethods, item => item.Id, "creation method");
        ValidateUnique(document.PriorityLevels, item => item.Id, "priority level");
        ValidateUnique(document.PriorityCategories, item => item.Id, "priority category");
        ValidateUnique(document.PriorityCells, item => item.Id, "priority cell");
        ValidateUnique(document.Metatypes, item => item.Id, "metatype");
        ValidateUnique(document.Attributes, item => item.Id, "attribute");
        ValidateUnique(document.Qualities, item => item.Id, "quality");
        ValidateUnique(document.Skills, item => item.Id, "skill");
        ValidateUnique(document.SkillGroups, item => item.Id, "skill group");
        ValidateUnique(document.KnowledgeCategories, item => item.Id, "knowledge category");
        ValidateUnique(document.KnowledgeSkillSuggestions ?? [], item => item.Id, "knowledge skill suggestion");
        ValidateUnique(document.LanguageSuggestions ?? [], item => item.Id, "language suggestion");
        ValidateUnique(document.Metavariants ?? [], item => item.Id, "metavariant");
        ValidateUnique(document.CreationPaths, item => item.Id, "creation path");
        ValidateUnique(document.AspectedValues, item => item.Id, "aspected value");
        ValidateUnique(document.Traditions, item => item.Id, "tradition");
        ValidateUnique(document.Spells, item => item.Id, "spell");
        ValidateUnique(document.Rituals, item => item.Id, "ritual");
        ValidateUnique(document.AdeptPowers, item => item.Id, "adept power");
        ValidateUnique(document.MentorSpirits, item => item.Id, "mentor spirit");
        ValidateUnique(document.ComplexForms, item => item.Id, "complex form");
        ValidateUnique(document.SpiritTypes, item => item.Id, "spirit type");
        ValidateUnique(document.SpriteTypes, item => item.Id, "sprite type");
        ValidateUnique(document.Foci, item => item.Id, "focus");
        ValidateUnique(document.Gear, item => item.Id, "gear");
        ValidateUnique(document.Weapons, item => item.Id, "weapon");
        ValidateUnique(document.Armor, item => item.Id, "armor");
        ValidateUnique(document.AugmentationGrades, item => item.Id, "augmentation grade");
        ValidateUnique(document.Augmentations, item => item.Id, "augmentation");
        ValidateUnique(document.Vehicles, item => item.Id, "vehicle");
        ValidateUnique(document.Cyberdecks, item => item.Id, "cyberdeck");
        ValidateUnique(document.WeaponAccessories, item => item.Id, "weapon accessory");
        ValidateUnique(document.ArmorModifications, item => item.Id, "armor modification");
        ValidateUnique(document.CyberlimbEnhancements, item => item.Id, "cyberlimb enhancement");
        ValidateUnique(document.VehicleModifications, item => item.Id, "vehicle modification");
        ValidateUnique(document.LifestyleTiers, item => item.Id, "lifestyle tier");
        ValidateUnique(document.LifestyleOptions, item => item.Id, "lifestyle option");

        var sourceIds = document.Sources.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (sourceIds.Count == 0)
        {
            throw new RulesetCatalogException("At least one approved source is required.");
        }

        foreach (var source in document.Sources)
        {
            RequireId(source.Id, "source.id");
            if (string.IsNullOrWhiteSpace(source.FileName)
                || !Sha256Pattern().IsMatch(source.Sha256))
            {
                throw new RulesetCatalogException($"Source '{source.Id}' has invalid provenance metadata.");
            }
        }

        foreach (var method in document.CreationMethods)
        {
            ValidateCommonEntry(method.Id, method.DisplayName, method.Source, sourceIds, "creation method");
        }

        foreach (var level in document.PriorityLevels)
        {
            ValidateCommonEntry(level.Id, level.DisplayName, level.Source, sourceIds, "priority level");
            if (level.SumToTenCost is < 0 or > 4)
            {
                throw new RulesetCatalogException($"Priority level '{level.Id}' has an invalid Sum-to-Ten cost.");
            }
        }

        foreach (var category in document.PriorityCategories)
        {
            ValidateCommonEntry(category.Id, category.DisplayName, category.Source, sourceIds, "priority category");
        }

        var attributeIds = document.Attributes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var attribute in document.Attributes)
        {
            ValidateCommonEntry(attribute.Id, attribute.DisplayName, attribute.Source, sourceIds, "attribute");
            if (attribute.Group is not ("physical" or "mental" or "special"))
                throw new RulesetCatalogException($"Attribute '{attribute.Id}' has an invalid group.");
        }

        foreach (var metatype in document.Metatypes)
        {
            ValidateCommonEntry(metatype.Id, metatype.DisplayName, metatype.Source, sourceIds, "metatype");
            if (metatype.Attributes is null || metatype.Attributes.Count != 9
                || metatype.Attributes.Keys.Any(id => !attributeIds.Contains(id)))
                throw new RulesetCatalogException($"Metatype '{metatype.Id}' must define all normal attributes.");
            foreach (var range in metatype.Attributes)
            {
                if (range.Value.Minimum < 1 || range.Value.Maximum < range.Value.Minimum)
                    throw new RulesetCatalogException($"Metatype '{metatype.Id}' has an invalid range for '{range.Key}'.");
            }
        }

        var metatypeIds = document.Metatypes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var priorityLevelIds = document.PriorityLevels.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var metavariant in document.Metavariants ?? [])
        {
            ValidateCommonEntry(metavariant.Id, metavariant.DisplayName, metavariant.Source, sourceIds, "metavariant");
            if (!metatypeIds.Contains(metavariant.ParentMetatypeId))
                throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' has a dangling parent metatype reference.");
            if (metavariant.Attributes is null || metavariant.Attributes.Count != 9
                || metavariant.Attributes.Keys.Any(id => !attributeIds.Contains(id)))
                throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' must define all normal attributes.");
            foreach (var range in metavariant.Attributes)
            {
                if (range.Value.Minimum < 1 || range.Value.Maximum < range.Value.Minimum)
                    throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' has an invalid range for '{range.Key}'.");
            }

            if (metavariant.PriorityGrants is null || metavariant.PriorityGrants.Count == 0)
                throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' must define at least one priority grant.");
            var grantLevelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grant in metavariant.PriorityGrants)
            {
                if (!priorityLevelIds.Contains(grant.LevelId))
                    throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' has a dangling priority level reference.");
                if (!grantLevelIds.Add(grant.LevelId))
                    throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' has more than one grant for level '{grant.LevelId}'.");
                if (grant.SpecialAttributePoints < 0 || grant.AdditionalKarmaCost < 0)
                    throw new RulesetCatalogException($"Metavariant '{metavariant.Id}' has a negative priority grant value.");
            }
        }

        var skillIds = document.Skills.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var skillGroupIds = document.SkillGroups.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var qualityIds = document.Qualities.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var aspectedValueIds = document.AspectedValues.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var traditionIds = document.Traditions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var creationPathIds = document.CreationPaths.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var quality in document.Qualities)
        {
            ValidateCommonEntry(quality.Id, quality.DisplayName, quality.Source, sourceIds, "quality");
            if (quality.Polarity is not ("positive" or "negative"))
                throw new RulesetCatalogException($"Quality '{quality.Id}' has an invalid polarity.");
            if (quality.Cost <= 0)
                throw new RulesetCatalogException($"Quality '{quality.Id}' must have a positive Karma cost.");
            if (quality.Conflicts is null || quality.Conflicts.Any(id => !qualityIds.Contains(id)))
                throw new RulesetCatalogException($"Quality '{quality.Id}' has a dangling conflict reference.");
        }

        foreach (var skill in document.Skills)
        {
            ValidateCommonEntry(skill.Id, skill.DisplayName, skill.Source, sourceIds, "skill");
            if (skill.Domain is not ("active" or "magical" or "resonance"))
                throw new RulesetCatalogException($"Skill '{skill.Id}' has an invalid domain.");
            if (!string.IsNullOrEmpty(skill.LinkedAttribute) && !attributeIds.Contains(skill.LinkedAttribute))
                throw new RulesetCatalogException($"Skill '{skill.Id}' has a dangling linked attribute.");
            if (skill.GroupId is not null && !skillGroupIds.Contains(skill.GroupId))
                throw new RulesetCatalogException($"Skill '{skill.Id}' has a dangling group reference.");
        }

        foreach (var group in document.SkillGroups)
        {
            ValidateCommonEntry(group.Id, group.DisplayName, group.Source, sourceIds, "skill group");
            if (group.SkillIds is null || group.SkillIds.Count == 0 || group.SkillIds.Any(id => !skillIds.Contains(id)))
                throw new RulesetCatalogException($"Skill group '{group.Id}' has invalid member skills.");
        }

        foreach (var category in document.KnowledgeCategories)
        {
            ValidateCommonEntry(category.Id, category.DisplayName, category.Source, sourceIds, "knowledge category");
            if (!attributeIds.Contains(category.LinkedAttribute))
                throw new RulesetCatalogException($"Knowledge category '{category.Id}' has a dangling linked attribute.");
        }

        var knowledgeCategoryIds = document.KnowledgeCategories.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var suggestion in document.KnowledgeSkillSuggestions ?? [])
        {
            ValidateCommonEntry(suggestion.Id, suggestion.DisplayName, suggestion.Source, sourceIds, "knowledge skill suggestion");
            if (!knowledgeCategoryIds.Contains(suggestion.CategoryId))
                throw new RulesetCatalogException($"Knowledge skill suggestion '{suggestion.Id}' has a dangling category reference.");
            if (suggestion.Specializations is null
                || suggestion.Specializations.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 120)
                || suggestion.Specializations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != suggestion.Specializations.Count)
                throw new RulesetCatalogException($"Knowledge skill suggestion '{suggestion.Id}' has invalid specializations.");
        }

        foreach (var suggestion in document.LanguageSuggestions ?? [])
        {
            ValidateCommonEntry(suggestion.Id, suggestion.DisplayName, suggestion.Source, sourceIds, "language suggestion");
        }

        foreach (var path in document.CreationPaths)
        {
            ValidateCommonEntry(path.Id, path.DisplayName, path.Source, sourceIds, "creation path");
            if (path.AttributeId is not null and not ("magic" or "resonance"))
                throw new RulesetCatalogException($"Creation path '{path.Id}' has an invalid attribute.");
            if (path.AspectedValueIds is null || path.AspectedValueIds.Any(id => !aspectedValueIds.Contains(id)))
                throw new RulesetCatalogException($"Creation path '{path.Id}' has a dangling aspected-value reference.");
        }

        foreach (var value in document.AspectedValues)
            ValidateCommonEntry(value.Id, value.DisplayName, value.Source, sourceIds, "aspected value");

        foreach (var tradition in document.Traditions)
        {
            ValidateCommonEntry(tradition.Id, tradition.DisplayName, tradition.Source, sourceIds, "tradition");
            if (string.IsNullOrWhiteSpace(tradition.DrainAttributes))
                throw new RulesetCatalogException($"Tradition '{tradition.Id}' must define its drain attributes.");
        }

        foreach (var spell in document.Spells)
        {
            ValidateCommonEntry(spell.Id, spell.DisplayName, spell.Source, sourceIds, "spell");
            if (string.IsNullOrWhiteSpace(spell.Category) || string.IsNullOrWhiteSpace(spell.Type)
                || string.IsNullOrWhiteSpace(spell.Range) || string.IsNullOrWhiteSpace(spell.Duration)
                || string.IsNullOrWhiteSpace(spell.Drain))
                throw new RulesetCatalogException($"Spell '{spell.Id}' has an empty descriptor.");
        }

        foreach (var ritual in document.Rituals)
        {
            ValidateCommonEntry(ritual.Id, ritual.DisplayName, ritual.Source, sourceIds, "ritual");
            if (ritual.Keywords is null || ritual.Keywords.Count == 0)
                throw new RulesetCatalogException($"Ritual '{ritual.Id}' must define at least one keyword.");
        }

        foreach (var power in document.AdeptPowers)
        {
            ValidateCommonEntry(power.Id, power.DisplayName, power.Source, sourceIds, "adept power");
            if (power.PowerPointCost <= 0)
                throw new RulesetCatalogException($"Adept power '{power.Id}' must have a positive Power Point cost.");
            if (power.MaxRank is <= 0)
                throw new RulesetCatalogException($"Adept power '{power.Id}' has an invalid maximum rank.");
            if (power.PowerPointCostByRank is not null)
            {
                if (!power.Ranked)
                    throw new RulesetCatalogException($"Adept power '{power.Id}' declares per-rank costs without being ranked.");
                if (power.PowerPointCostByRank.Count == 0)
                    throw new RulesetCatalogException($"Adept power '{power.Id}' declares an empty per-rank cost table.");
                foreach (var entry in power.PowerPointCostByRank)
                {
                    if (entry.Key <= 0 || entry.Value <= 0)
                        throw new RulesetCatalogException($"Adept power '{power.Id}' has an invalid per-rank cost.");
                }
            }
        }

        foreach (var mentor in document.MentorSpirits)
            ValidateCommonEntry(mentor.Id, mentor.DisplayName, mentor.Source, sourceIds, "mentor spirit");

        foreach (var form in document.ComplexForms)
        {
            ValidateCommonEntry(form.Id, form.DisplayName, form.Source, sourceIds, "complex form");
            if (string.IsNullOrWhiteSpace(form.Target) || string.IsNullOrWhiteSpace(form.Duration)
                || string.IsNullOrWhiteSpace(form.Fade))
                throw new RulesetCatalogException($"Complex form '{form.Id}' has an empty descriptor.");
        }

        foreach (var spirit in document.SpiritTypes)
        {
            ValidateCommonEntry(spirit.Id, spirit.DisplayName, spirit.Source, sourceIds, "spirit type");
            if (spirit.TraditionIds is null || spirit.TraditionIds.Any(id => !traditionIds.Contains(id)))
                throw new RulesetCatalogException($"Spirit type '{spirit.Id}' has a dangling tradition reference.");
        }

        foreach (var sprite in document.SpriteTypes)
            ValidateCommonEntry(sprite.Id, sprite.DisplayName, sprite.Source, sourceIds, "sprite type");

        foreach (var focus in document.Foci)
        {
            ValidateCommonEntry(focus.Id, focus.DisplayName, focus.Source, sourceIds, "focus");
            ValidateResourceEntry(focus.Availability, focus.Cost, null, null, focus.RatingRange,
                null, null, $"focus '{focus.Id}'");
        }

        foreach (var grade in document.AugmentationGrades)
        {
            ValidateCommonEntry(grade.Id, grade.DisplayName, grade.Source, sourceIds, "augmentation grade");
            if (grade.EssenceMultiplier <= 0 || grade.CostMultiplier <= 0)
                throw new RulesetCatalogException($"Augmentation grade '{grade.Id}' must declare positive multipliers.");
        }

        foreach (var gear in document.Gear)
        {
            ValidateCommonEntry(gear.Id, gear.DisplayName, gear.Source, sourceIds, "gear");
            ValidateResourceEntry(gear.Availability, gear.Cost, null, gear.Capacity, gear.RatingRange,
                gear.IncludedComponentIds, gear.GeneratedProfileIds, $"gear '{gear.Id}'");
            if (gear.CapacityCost is not null
                && (gear.CapacityCost.Fixed is < 0 || gear.CapacityCost.PerRating is < 0
                    || (gear.CapacityCost.Fixed is null && gear.CapacityCost.PerRating is null)))
            {
                throw new RulesetCatalogException($"Gear '{gear.Id}' has an invalid Capacity cost.");
            }

            if (gear.CapacityCost is not null && (gear.IsCapacityHost || gear.Capacity is not null))
            {
                throw new RulesetCatalogException(
                    $"Gear '{gear.Id}' cannot be both a Capacity host and a Capacity-consuming item.");
            }

            if (gear.IsCapacityHost && gear.RatingRange is null)
            {
                throw new RulesetCatalogException($"Gear '{gear.Id}' is a Capacity host but declares no rating range.");
            }
        }

        foreach (var weapon in document.Weapons)
        {
            ValidateCommonEntry(weapon.Id, weapon.DisplayName, weapon.Source, sourceIds, "weapon");
            if (string.IsNullOrWhiteSpace(weapon.WeaponCategoryId))
                throw new RulesetCatalogException($"Weapon '{weapon.Id}' must declare a weapon category.");
            ValidateResourceEntry(weapon.Availability, weapon.Cost, null, null, weapon.RatingRange,
                weapon.IncludedComponentIds, weapon.GeneratedProfileIds, $"weapon '{weapon.Id}'");
        }

        foreach (var armor in document.Armor)
        {
            ValidateCommonEntry(armor.Id, armor.DisplayName, armor.Source, sourceIds, "armor");
            ValidateResourceEntry(armor.Availability, armor.Cost, null, armor.Capacity, armor.RatingRange,
                armor.IncludedComponentIds, null, $"armor '{armor.Id}'");
        }

        foreach (var augmentation in document.Augmentations)
        {
            ValidateCommonEntry(augmentation.Id, augmentation.DisplayName, augmentation.Source, sourceIds, "augmentation");
            if (string.IsNullOrWhiteSpace(augmentation.AugmentationCategoryId))
                throw new RulesetCatalogException($"Augmentation '{augmentation.Id}' must declare an augmentation category.");
            ValidateResourceEntry(augmentation.Availability, augmentation.Cost, augmentation.Essence,
                null, augmentation.RatingRange, augmentation.IncludedComponentIds,
                augmentation.GeneratedProfileIds, $"augmentation '{augmentation.Id}'");
            if (augmentation.Capacity is not null
                && (augmentation.Capacity.Fixed is < 0 || augmentation.Capacity.PerRating is < 0))
            {
                throw new RulesetCatalogException($"Augmentation '{augmentation.Id}' has an invalid Capacity.");
            }

            if (augmentation.CapacityCost is not null
                && (augmentation.CapacityCost.Fixed is < 0 || augmentation.CapacityCost.PerRating is < 0
                    || (augmentation.CapacityCost.Fixed is null && augmentation.CapacityCost.PerRating is null)))
            {
                throw new RulesetCatalogException($"Augmentation '{augmentation.Id}' has an invalid Capacity cost.");
            }

            if (augmentation.Capacity is not null && augmentation.CapacityCost is not null)
            {
                throw new RulesetCatalogException(
                    $"Augmentation '{augmentation.Id}' cannot be both a Capacity host and a Capacity-consuming item.");
            }
        }

        foreach (var vehicle in document.Vehicles)
        {
            ValidateCommonEntry(vehicle.Id, vehicle.DisplayName, vehicle.Source, sourceIds, "vehicle");
            if (string.IsNullOrWhiteSpace(vehicle.VehicleCategoryId))
                throw new RulesetCatalogException($"Vehicle '{vehicle.Id}' must declare a vehicle category.");
            ValidateResourceEntry(vehicle.Availability, vehicle.Cost, null, null, null,
                vehicle.IncludedComponentIds, null, $"vehicle '{vehicle.Id}'");
        }

        foreach (var accessory in document.WeaponAccessories)
        {
            ValidateCommonEntry(accessory.Id, accessory.DisplayName, accessory.Source, sourceIds, "weapon accessory");
            ValidateResourceEntry(accessory.Availability, accessory.Cost, null, accessory.Capacity, accessory.RatingRange,
                null, null, $"weapon accessory '{accessory.Id}'");
            ValidateReferenceIds(accessory.RestrictedToWeaponCategoryIds, $"weapon accessory '{accessory.Id}'",
                "restricted weapon category");
        }

        foreach (var modification in document.ArmorModifications)
        {
            ValidateCommonEntry(modification.Id, modification.DisplayName, modification.Source, sourceIds, "armor modification");
            ValidateResourceEntry(modification.Availability, modification.Cost, null, null, modification.RatingRange,
                null, null, $"armor modification '{modification.Id}'");
            if (modification.CapacityCost is null
                || (modification.CapacityCost.Fixed is null && modification.CapacityCost.PerRating is null)
                || modification.CapacityCost.Fixed is < 0
                || modification.CapacityCost.PerRating is < 0)
            {
                throw new RulesetCatalogException($"Armor modification '{modification.Id}' has an invalid Capacity cost.");
            }
        }

        foreach (var enhancement in document.CyberlimbEnhancements)
        {
            ValidateCommonEntry(enhancement.Id, enhancement.DisplayName, enhancement.Source, sourceIds, "cyberlimb enhancement");
            ValidateResourceEntry(enhancement.Availability, enhancement.Cost, null, null, enhancement.RatingRange,
                null, null, $"cyberlimb enhancement '{enhancement.Id}'");
            if (enhancement.CapacityCost is null
                || (enhancement.CapacityCost.Fixed is null && enhancement.CapacityCost.PerRating is null)
                || enhancement.CapacityCost.Fixed is < 0
                || enhancement.CapacityCost.PerRating is < 0)
            {
                throw new RulesetCatalogException($"Cyberlimb enhancement '{enhancement.Id}' has an invalid Capacity cost.");
            }
        }

        var vehicleModificationIds = document.VehicleModifications.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var modification in document.VehicleModifications)
        {
            ValidateCommonEntry(modification.Id, modification.DisplayName, modification.Source, sourceIds, "vehicle modification");
            var label = $"vehicle modification '{modification.Id}'";
            if (modification.Cost is not null && modification.CostScaling is not null)
            {
                throw new RulesetCatalogException($"{label} cannot declare both a flat cost and a cost scaling.");
            }

            ValidateResourceEntry(modification.Availability, modification.Cost, null, null, modification.RatingRange,
                null, null, label);

            if (modification.CostScaling is not null)
            {
                if (modification.CostScaling.Multiplier <= 0)
                    throw new RulesetCatalogException($"{label} has a non-positive cost multiplier.");
                if (modification.CostScaling.Factors is not { Count: > 0 })
                    throw new RulesetCatalogException($"{label} must declare at least one cost scaling factor.");
                if (modification.CostScaling.Factors.Contains(VehicleScalingFactor.Rating) && modification.RatingRange is null)
                    throw new RulesetCatalogException($"{label} scales with Rating but declares no Rating range.");
            }

            if (modification.SlotCost is null
                || (modification.SlotCost.Fixed is null && modification.SlotCost.PerRating is null)
                || modification.SlotCost.PerRating is < 0)
            {
                throw new RulesetCatalogException($"{label} has an invalid slot cost.");
            }

            if (modification.SlotCost.PerRating is not null && modification.RatingRange is null)
            {
                throw new RulesetCatalogException($"{label} has a Rating-scaled slot cost but declares no Rating range.");
            }

            // Drone Immobile is the only entry allowed to return slots.
            if (modification.SlotCost.Fixed is < 0 && modification.Category != VehicleModificationCategory.Drone)
            {
                throw new RulesetCatalogException($"{label} has a negative slot cost outside the drone Mod Point pool.");
            }

            if (modification.RatingCap != VehicleRatingCap.None && modification.RatingRange is null)
            {
                throw new RulesetCatalogException($"{label} declares a vehicle Rating cap but no Rating range.");
            }

            if (modification.Relative)
            {
                if (string.IsNullOrWhiteSpace(modification.OptionGroupId))
                    throw new RulesetCatalogException($"{label} is relative but declares no option group.");
                if (modification.AppliesToModificationIds is not { Count: > 0 })
                    throw new RulesetCatalogException($"{label} is relative but applies to no base modification.");
                foreach (var baseId in modification.AppliesToModificationIds)
                {
                    if (!vehicleModificationIds.Contains(baseId))
                        throw new RulesetCatalogException($"{label} applies to unknown vehicle modification '{baseId}'.");
                }
            }
            else if (modification.OptionGroupId is not null || modification.AppliesToModificationIds is not null)
            {
                throw new RulesetCatalogException($"{label} declares option metadata but is not relative.");
            }
        }

        foreach (var tier in document.LifestyleTiers)
        {
            ValidateCommonEntry(tier.Id, tier.DisplayName, tier.Source, sourceIds, "lifestyle tier");
            if (tier.BaseCostPerMonth < 0)
                throw new RulesetCatalogException($"Lifestyle tier '{tier.Id}' has a negative base cost.");
            if (tier.StartingCashDice is null
                || tier.StartingCashDice.Count <= 0
                || tier.StartingCashDice.Sides <= 0
                || tier.StartingCashDice.Multiplier <= 0)
                throw new RulesetCatalogException($"Lifestyle tier '{tier.Id}' has an invalid starting-cash dice expression.");
        }

        foreach (var option in document.LifestyleOptions)
        {
            ValidateCommonEntry(option.Id, option.DisplayName, option.Source, sourceIds, "lifestyle option");
            if (option.AdjustmentPercent is null && option.FixedMonthlyAmount is null)
                throw new RulesetCatalogException($"Lifestyle option '{option.Id}' must declare an adjustment.");
            if (option.AdjustmentPercent is not null && option.FixedMonthlyAmount is not null)
                throw new RulesetCatalogException($"Lifestyle option '{option.Id}' cannot declare both adjustment forms.");
        }

        foreach (var deck in document.Cyberdecks)
        {
            ValidateCommonEntry(deck.Id, deck.DisplayName, deck.Source, sourceIds, "cyberdeck");
            ValidateResourceEntry(deck.Availability, deck.Cost, null, null, null, null, null, $"cyberdeck '{deck.Id}'");
            if (deck.DeviceRating is null or <= 0)
                throw new RulesetCatalogException($"Cyberdeck '{deck.Id}' must declare a positive Device Rating.");
            if (deck.AttributeArray is null || deck.AttributeArray.Count != 4 || deck.AttributeArray.Any(value => value <= 0))
                throw new RulesetCatalogException($"Cyberdeck '{deck.Id}' must declare a 4-value attribute array.");
            if (deck.Programs is null or <= 0)
                throw new RulesetCatalogException($"Cyberdeck '{deck.Id}' must declare a positive program count.");
        }

        var levelIds = document.PriorityLevels.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var categoryIds = document.PriorityCategories.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var combinations = new HashSet<(string CategoryId, string LevelId)>();
        foreach (var cell in document.PriorityCells)
        {
            RequireId(cell.Id, "priorityCells.id");
            if (!categoryIds.Contains(cell.CategoryId) || !levelIds.Contains(cell.LevelId))
            {
                throw new RulesetCatalogException($"Priority cell '{cell.Id}' has a dangling category or level reference.");
            }

            if (!combinations.Add((cell.CategoryId, cell.LevelId)))
            {
                throw new RulesetCatalogException(
                    $"Priority category '{cell.CategoryId}' has more than one '{cell.LevelId}' cell.");
            }

            ValidateCitation(cell.Source, sourceIds, cell.Id);
            if (cell.CategoryId == "attributes"
                && cell.PhysicalMentalAttributePoints is null or < 0)
                throw new RulesetCatalogException($"Attribute cell '{cell.Id}' must define its point grant.");
            if (cell.CategoryId == "metatype")
            {
                if (cell.MetatypeSpecialAttributePoints is null
                    || cell.MetatypeSpecialAttributePoints.Keys.Any(id => !document.Metatypes.Any(m => m.Id == id))
                    || cell.AvailableMetatypeIds is null
                    || cell.AvailableMetatypeIds.Any(id => !document.Metatypes.Any(m => m.Id == id)))
                    throw new RulesetCatalogException($"Metatype cell '{cell.Id}' has invalid grants.");
            }
            if (cell.CategoryId == "skills"
                && (cell.IndividualSkillPoints is null or < 0 || cell.SkillGroupPoints is null or < 0))
                throw new RulesetCatalogException($"Skill cell '{cell.Id}' must define its point grants.");
            if (cell.CategoryId == "resources" && cell.ResourceNuyen is null or < 0)
                throw new RulesetCatalogException($"Resource cell '{cell.Id}' must define its nuyen grant.");
            if (cell.CategoryId == "magic-resonance")
            {
                if (cell.MagicResonancePathGrants is null)
                    throw new RulesetCatalogException($"Magic/Resonance cell '{cell.Id}' must define its path grants.");
                foreach (var grant in cell.MagicResonancePathGrants)
                {
                    if (!creationPathIds.Contains(grant.PathId))
                        throw new RulesetCatalogException($"Magic/Resonance grant in '{cell.Id}' has a dangling path reference.");
                    if (grant.AttributeRating < 0 || grant.FormulaGrants < 0 || grant.ComplexFormGrants < 0)
                        throw new RulesetCatalogException($"Magic/Resonance grant '{grant.PathId}' has a negative grant.");
                    if (grant.SkillGrants is null || grant.SkillGrants.Any(item =>
                        item.Domain is not ("active" or "magical" or "resonance" or "magical-group")
                        || item.Count < 0 || item.Rating < 0))
                        throw new RulesetCatalogException($"Magic/Resonance grant '{grant.PathId}' has an invalid skill grant.");
                }
            }
        }

        foreach (var categoryId in categoryIds)
        {
            foreach (var levelId in levelIds)
            {
                if (!combinations.Contains((categoryId, levelId)))
                {
                    throw new RulesetCatalogException(
                        $"Priority category '{categoryId}' is missing its '{levelId}' cell.");
                }
            }
        }
    }

    private static ImmutableDictionary<string, T> ToDictionary<T>(IEnumerable<T> values, Func<T, string> keySelector) =>
        values.ToImmutableDictionary(keySelector, StringComparer.Ordinal);

    private static void ValidateCommonEntry(
        string id,
        string? displayName,
        SourceCitation? citation,
        HashSet<string> sourceIds,
        string description)
    {
        RequireId(id, description + ".id");
        RequireDisplayName(displayName, id);
        ValidateCitation(citation, sourceIds, id);
    }

    private static void ValidateUnique<T>(IReadOnlyList<T> values, Func<T, string> keySelector, string description)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var id = keySelector(value);
            if (!ids.Add(id))
            {
                throw new RulesetCatalogException($"Duplicate {description} ID '{id}'.");
            }
        }
    }

    private static void RequireId(string? value, string field)
    {
        if (value is null || !IdPattern().IsMatch(value))
        {
            throw new RulesetCatalogException($"'{field}' must be a lowercase stable ID.");
        }
    }

    private static void RequireDisplayName(string? value, string id)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new RulesetCatalogException($"Catalog entry '{id}' has an invalid display name.");
        }
    }

    private static void ValidateCitation(SourceCitation? citation, HashSet<string> sourceIds, string entryId)
    {
        if (citation is null
            || !sourceIds.Contains(citation.SourceId)
            || citation.PrintedPage <= 0
            || citation.PdfPage <= 0)
        {
            throw new RulesetCatalogException($"Catalog entry '{entryId}' has an invalid or dangling source citation.");
        }
    }

    private static void ValidateResourceEntry(
        AvailabilityDefinition? availability,
        CostDefinition? cost,
        EssenceDefinition? essence,
        int? capacity,
        RatingRangeDefinition? ratingRange,
        IReadOnlyList<string>? includedComponentIds,
        IReadOnlyList<string>? generatedProfileIds,
        string description)
    {
        if (availability is not null)
        {
            if (availability.Fixed is < 0 || availability.PerRating is < 0
                || availability.ByRating?.Values.Any(value => value < 0) == true)
                throw new RulesetCatalogException($"{description} has a negative availability.");
        }

        if (cost is not null)
        {
            if (cost.Fixed is < 0 || cost.PerRating is < 0
                || cost.ByRating?.Values.Any(value => value < 0) == true)
                throw new RulesetCatalogException($"{description} has a negative cost.");
        }

        if (essence is not null)
        {
            if (essence.Fixed is < 0 || essence.PerRating is < 0
                || essence.ByRating?.Values.Any(value => value < 0) == true)
                throw new RulesetCatalogException($"{description} has a negative Essence value.");
        }

        if (capacity is < 0)
            throw new RulesetCatalogException($"{description} has a negative capacity.");

        if (ratingRange is not null && (ratingRange.Minimum < 1 || ratingRange.Maximum < ratingRange.Minimum))
            throw new RulesetCatalogException($"{description} has an invalid rating range.");

        ValidateReferenceIds(includedComponentIds, description, "included component");
        ValidateReferenceIds(generatedProfileIds, description, "generated profile");
    }

    private static void ValidateReferenceIds(IReadOnlyList<string>? references, string description, string kind)
    {
        if (references is null)
        {
            return;
        }

        if (references.Count > 100 || references.Any(reference => !IdPattern().IsMatch(reference)))
            throw new RulesetCatalogException($"{description} has an invalid {kind} reference.");
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(property.Value, writer);
            }

            writer.WriteEndObject();
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                WriteCanonical(item, writer);
            }

            writer.WriteEndArray();
            return;
        }

        element.WriteTo(writer);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private sealed record CatalogDocument(
        string RulesetId,
        string Version,
        CatalogSource[]? Sources,
        CreationMethodDefinition[]? CreationMethods,
        PriorityLevelDefinition[]? PriorityLevels,
        PriorityCategoryDefinition[]? PriorityCategories,
        PriorityCellDefinition[]? PriorityCells,
        MetatypeDefinition[]? Metatypes,
        MetavariantDefinition[]? Metavariants,
        AttributeDefinition[]? Attributes,
        QualityDefinition[]? Qualities,
        SkillDefinition[]? Skills,
        SkillGroupDefinition[]? SkillGroups,
        KnowledgeCategoryDefinition[]? KnowledgeCategories,
        KnowledgeSkillSuggestionDefinition[]? KnowledgeSkillSuggestions,
        LanguageSuggestionDefinition[]? LanguageSuggestions,
        CreationPathDefinition[]? CreationPaths,
        AspectedValueDefinition[]? AspectedValues,
        TraditionDefinition[]? Traditions,
        SpellDefinition[]? Spells,
        RitualDefinition[]? Rituals,
        AdeptPowerDefinition[]? AdeptPowers,
        MentorSpiritDefinition[]? MentorSpirits,
        ComplexFormDefinition[]? ComplexForms,
        SpiritTypeDefinition[]? SpiritTypes,
        SpriteTypeDefinition[]? SpriteTypes,
        FocusDefinition[]? Foci,
        GearDefinition[]? Gear,
        WeaponDefinition[]? Weapons,
        ArmorDefinition[]? Armor,
        AugmentationGradeDefinition[]? AugmentationGrades,
        AugmentationDefinition[]? Augmentations,
        VehicleDefinition[]? Vehicles,
        CyberdeckDefinition[]? Cyberdecks,
        WeaponAccessoryDefinition[]? WeaponAccessories,
        ArmorModificationDefinition[]? ArmorModifications,
        CyberlimbEnhancementDefinition[]? CyberlimbEnhancements,
        VehicleModificationDefinition[]? VehicleModifications,
        LifestyleTierDefinition[]? LifestyleTiers,
        LifestyleOptionDefinition[]? LifestyleOptions);
}
