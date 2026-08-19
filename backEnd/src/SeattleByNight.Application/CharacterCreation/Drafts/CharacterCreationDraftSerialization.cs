using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public static class CharacterCreationDraftSerialization
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string SerializeDocument(CharacterCreationDraftDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static CharacterCreationDraftDocument DeserializeDocument(string json) =>
        JsonSerializer.Deserialize<CharacterCreationDraftDocument>(json, Options)
        ?? throw new JsonException("The draft document is empty.");

    public static string DigestDocument(CharacterCreationDraftDocument document) =>
        RulesetCatalogLoader.ComputeSemanticDigest(SerializeDocument(document));

    public static string SerializeCanonicalSheet(CanonicalCharacterSheet sheet) =>
        JsonSerializer.Serialize(sheet, Options);

    public static CanonicalCharacterSheet DeserializeCanonicalSheet(string json) =>
        JsonSerializer.Deserialize<CanonicalCharacterSheet>(json, Options)
        ?? throw new JsonException("The canonical sheet is empty.");
}
