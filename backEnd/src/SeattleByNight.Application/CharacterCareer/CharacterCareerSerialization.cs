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
}
