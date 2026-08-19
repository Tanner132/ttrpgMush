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
            ToDictionary(document.PriorityCells!.Select(item => item.CategoryId == "skills"
                ? item with { IndividualSkillPoints = item.LevelId switch { "a" => 46, "b" => 36, "c" => 28, "d" => 22, _ => 18 }, SkillGroupPoints = item.LevelId switch { "a" => 10, "b" => 5, "c" => 2, _ => 0 } }
                : item), item => item.Id),
            ToDictionary(document.Metatypes!, item => item.Id),
            ToDictionary(document.Attributes!, item => item.Id),
            BuildQualities(document.Sources!.First(item => item.Id == "sr5-core")),
            BuildSkills(document.Sources!.First(item => item.Id == "sr5-core")),
            BuildSkillGroups(document.Sources!.First(item => item.Id == "sr5-core")),
            BuildKnowledgeCategories(document.Sources!.First(item => item.Id == "sr5-core")));
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
            || document.Attributes is null)
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
            RequireId(method.Id, "creationMethods.id");
            RequireDisplayName(method.DisplayName, method.Id);
            ValidateCitation(method.Source, sourceIds, method.Id);
        }

        foreach (var level in document.PriorityLevels)
        {
            RequireId(level.Id, "priorityLevels.id");
            RequireDisplayName(level.DisplayName, level.Id);
            if (level.SumToTenCost is < 0 or > 4)
            {
                throw new RulesetCatalogException($"Priority level '{level.Id}' has an invalid Sum-to-Ten cost.");
            }

            ValidateCitation(level.Source, sourceIds, level.Id);
        }

        foreach (var category in document.PriorityCategories)
        {
            RequireId(category.Id, "priorityCategories.id");
            RequireDisplayName(category.DisplayName, category.Id);
            ValidateCitation(category.Source, sourceIds, category.Id);
        }

        var attributeIds = document.Attributes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var attribute in document.Attributes)
        {
            RequireId(attribute.Id, "attributes.id");
            RequireDisplayName(attribute.DisplayName, attribute.Id);
            if (attribute.Group is not ("physical" or "mental" or "special"))
                throw new RulesetCatalogException($"Attribute '{attribute.Id}' has an invalid group.");
            ValidateCitation(attribute.Source, sourceIds, attribute.Id);
        }

        foreach (var metatype in document.Metatypes)
        {
            RequireId(metatype.Id, "metatypes.id");
            RequireDisplayName(metatype.DisplayName, metatype.Id);
            ValidateCitation(metatype.Source, sourceIds, metatype.Id);
            if (metatype.Attributes is null || metatype.Attributes.Count != 9
                || metatype.Attributes.Keys.Any(id => !attributeIds.Contains(id)))
                throw new RulesetCatalogException($"Metatype '{metatype.Id}' must define all normal attributes.");
            foreach (var range in metatype.Attributes)
            {
                if (range.Value.Minimum < 1 || range.Value.Maximum < range.Value.Minimum)
                    throw new RulesetCatalogException($"Metatype '{metatype.Id}' has an invalid range for '{range.Key}'.");
            }
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
                && (cell.PhysicalMentalAttributePoints is null or < 0))
                throw new RulesetCatalogException($"Attribute cell '{cell.Id}' must define its point grant.");
            if (cell.CategoryId == "metatype")
            {
                if (cell.MetatypeSpecialAttributePoints is null
                    || cell.MetatypeSpecialAttributePoints.Keys.Any(id => !document.Metatypes.Any(m => m.Id == id))
                    || cell.AvailableMetatypeIds is null
                    || cell.AvailableMetatypeIds.Any(id => !document.Metatypes.Any(m => m.Id == id)))
                    throw new RulesetCatalogException($"Metatype cell '{cell.Id}' has invalid grants.");
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

    private static ImmutableDictionary<string, QualityDefinition> BuildQualities(CatalogSource source)
    {
        var positive = new (string Id, int Cost, bool Parameterized, bool Repeatable)[]
        {
            ("ambidextrous",4,false,false),("analytical-mind",5,false,false),("aptitude",14,true,false),("astral-chameleon",10,false,false),("bilingual",5,true,false),("blandness",8,false,false),("catlike",7,false,false),("codeslinger",10,true,false),("double-jointed",6,false,false),("exceptional-attribute",14,true,false),("first-impression",11,false,false),("focused-concentration",4,true,false),("gearhead",11,false,false),("guts",10,false,false),("high-pain-tolerance",7,true,false),("home-ground",10,true,true),("human-looking",6,false,false),("indomitable",8,true,false),("juryrigger",10,false,false),("lucky",12,false,false),("magic-resistance",6,true,false),("mentor-spirit",5,true,false),("natural-athlete",7,false,false),("natural-hardening",10,false,false),("natural-immunity",4,true,false),("photographic-memory",6,false,false),("quick-healer",3,false,false),("resistance-to-pathogens-toxins",4,true,false),("spirit-affinity",7,true,false),("toughness",9,false,false),("will-to-live",3,true,false)
        };
        var negative = new (string Id, int Cost, bool Parameterized, bool Repeatable)[]
        {
            ("addiction",4,true,false),("allergy",5,true,false),("astral-beacon",10,false,false),("bad-luck",12,false,false),("bad-rep",7,false,false),("code-of-honor",15,true,false),("codeblock",10,true,false),("combat-paralysis",12,false,false),("dependents",3,true,false),("distinctive-style",5,true,false),("elf-poser",6,false,false),("gremlins",4,true,false),("incompetent",5,true,false),("insomnia",10,true,false),("loss-of-confidence",10,true,false),("low-pain-tolerance",9,false,false),("ork-poser",6,false,false),("prejudiced",3,true,false),("scorched",10,true,false),("sensitive-system",12,false,false),("simsense-vertigo",5,false,false),("sinner-layered",5,true,false),("social-stress",8,true,false),("spirit-bane",7,true,false),("uncouth",14,false,false),("uneducated",8,false,false),("unsteady-hands",7,false,false),("weak-immune-system",10,false,false)
        };
        return positive.Select(item => new QualityDefinition(item.Id, Display(item.Id), "positive", item.Cost, item.Parameterized, item.Repeatable, Conflicts(item.Id), Citation(source, 71)))
            .Concat(negative.Select(item => new QualityDefinition(item.Id, Display(item.Id), "negative", item.Cost, item.Parameterized, item.Repeatable, Conflicts(item.Id), Citation(source, 77))))
            .ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private static ImmutableDictionary<string, SkillDefinition> BuildSkills(CatalogSource source)
    {
        const string ids = "archery automatics blades clubs escape-artist exotic-melee-weapon exotic-ranged-weapon gunnery gymnastics heavy-weapons locksmith longarms palming pistols sneaking throwing-weapons unarmed-combat diving free-fall pilot-aerospace pilot-aircraft pilot-walker pilot-exotic-vehicle pilot-ground-craft pilot-watercraft running swimming animal-handling con etiquette impersonation instruction intimidation leadership negotiation performance artisan assensing disguise navigation perception tracking aeronautics-mechanic arcana armorer automotive-mechanic biotechnology chemistry computer cybercombat cybertechnology demolitions electronic-warfare first-aid forgery hacking hardware industrial-mechanic medicine nautical-mechanic software astral-combat survival alchemy artificing banishing binding counterspelling disenchanting ritual-spellcasting spellcasting summoning compiling decompiling registering";
        var groups = BuildSkillGroupMemberships();
        return ids.Split(' ').Select(id => new SkillDefinition(id, Display(id), "active", "", groups.FirstOrDefault(item => item.Value.Contains(id)).Key, id.Contains("exotic") || id.StartsWith("pilot-exotic"), Citation(source, 131)))
            .ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private static ImmutableDictionary<string, SkillGroupDefinition> BuildSkillGroups(CatalogSource source) =>
        BuildSkillGroupMemberships().Select(item => new SkillGroupDefinition(item.Key, Display(item.Key), item.Value, Citation(source, 153)))
            .ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);

    private static ImmutableDictionary<string, KnowledgeCategoryDefinition> BuildKnowledgeCategories(CatalogSource source) =>
        new[] { new KnowledgeCategoryDefinition("academic", "Academic", "logic", Citation(source, 148)), new("interests", "Interests", "intuition", Citation(source, 148)), new("professional", "Professional", "logic", Citation(source, 148)), new("street", "Street", "intuition", Citation(source, 148)) }
            .ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);

    private static Dictionary<string, string[]> BuildSkillGroupMemberships() => new(StringComparer.Ordinal)
    {
        ["acting"] = ["con", "impersonation", "performance"], ["athletics"] = ["gymnastics", "running", "swimming"], ["biotech"] = ["cybertechnology", "first-aid", "medicine"], ["close-combat"] = ["blades", "clubs", "unarmed-combat"], ["conjuring"] = ["banishing", "binding", "summoning"], ["cracking"] = ["cybercombat", "electronic-warfare", "hacking"], ["electronics"] = ["computer", "hardware", "software"], ["enchanting"] = ["alchemy", "artificing", "disenchanting"], ["engineering"] = ["aeronautics-mechanic", "automotive-mechanic", "industrial-mechanic", "nautical-mechanic"], ["firearms"] = ["automatics", "longarms", "pistols"], ["influence"] = ["etiquette", "leadership", "negotiation"], ["outdoors"] = ["navigation", "survival", "tracking"], ["sorcery"] = ["counterspelling", "ritual-spellcasting", "spellcasting"], ["stealth"] = ["disguise", "palming", "sneaking"], ["tasking"] = ["compiling", "decompiling", "registering"]
    };

    private static SourceCitation Citation(CatalogSource source, int page) => new(source.Id, page, page + 2);
    private static string Display(string id) => string.Join(' ', id.Split('-').Select(item => char.ToUpperInvariant(item[0]) + item[1..]));
    private static IReadOnlyList<string> Conflicts(string id) => id switch { "blandness" => ["distinctive-style"], "distinctive-style" => ["blandness"], "lucky" => ["exceptional-attribute"], "exceptional-attribute" => ["lucky"], "natural-immunity" => ["weak-immune-system"], "weak-immune-system" => ["natural-immunity", "resistance-to-pathogens-toxins"], _ => [] };

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
        AttributeDefinition[]? Attributes);
}
