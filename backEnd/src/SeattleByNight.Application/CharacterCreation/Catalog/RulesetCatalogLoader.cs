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
            ToDictionary(document.PriorityCells!, item => item.Id));
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
            || document.PriorityCells is null)
        {
            throw new RulesetCatalogException("The catalog is missing a required collection.");
        }

        ValidateUnique(document.Sources, item => item.Id, "source");
        ValidateUnique(document.CreationMethods, item => item.Id, "creation method");
        ValidateUnique(document.PriorityLevels, item => item.Id, "priority level");
        ValidateUnique(document.PriorityCategories, item => item.Id, "priority category");
        ValidateUnique(document.PriorityCells, item => item.Id, "priority cell");

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
        PriorityCellDefinition[]? PriorityCells);
}
