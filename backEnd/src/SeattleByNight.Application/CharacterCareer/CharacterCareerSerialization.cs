using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeattleByNight.Application.CharacterCareer;

public static class CharacterCareerSerialization
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string SerializeProgression(CareerProgressionDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static CareerProgressionDocument DeserializeProgression(string json) =>
        JsonSerializer.Deserialize<CareerProgressionDocument>(json, Options)
        ?? throw new JsonException("The career progression document is empty.");

    public static string SerializeReceipt(CharacterActionReceiptPayload payload) =>
        JsonSerializer.Serialize(payload, Options);

    public static CharacterActionReceiptPayload DeserializeReceipt(string json) =>
        JsonSerializer.Deserialize<CharacterActionReceiptPayload>(json, Options)
        ?? throw new JsonException("The character action receipt is empty.");
}

// Envelope stored in character_action_receipts.result_json. Kind guards
// against a client replaying a request-id that was originally minted by a
// different career command (see CharacterCareerActionKinds).
public sealed record CharacterActionReceiptPayload(string Kind, JsonElement Result);
