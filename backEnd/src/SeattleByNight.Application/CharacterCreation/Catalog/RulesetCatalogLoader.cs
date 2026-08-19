using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        if (expectedSemanticDigest is not null
            && !string.Equals(digest, expectedSemanticDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new RulesetCatalogException(
                $"Catalog semantic digest mismatch. Expected {expectedSemanticDigest}, calculated {digest}.");
        }

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
            ToDictionary(document.Attributes!, item => item.Id),
            ToDictionary(document.Qualities!, item => item.Id),
            ToDictionary(document.Skills!, item => item.Id),
            ToDictionary(document.SkillGroups!, item => item.Id),
            ToDictionary(document.KnowledgeCategories!, item => item.Id),
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
            ToDictionary(document.Foci!, item => item.Id));
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
            || document.Foci is null)
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
            ValidateCommonEntry(focus.Id, focus.DisplayName, focus.Source, sourceIds, "focus");

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
        AttributeDefinition[]? Attributes,
        QualityDefinition[]? Qualities,
        SkillDefinition[]? Skills,
        SkillGroupDefinition[]? SkillGroups,
        KnowledgeCategoryDefinition[]? KnowledgeCategories,
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
        FocusDefinition[]? Foci);
}
