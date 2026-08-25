using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SeattleByNight.Api.Tests;

public sealed class CharacterCareerSheetEndpointTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";
    private readonly ApiTestFactory factory;

    public CharacterCareerSheetEndpointTests(ApiTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task Career_sheet_requires_authentication()
    {
        var anonymous = await factory.CreateClient().GetAsync($"/api/characters/{Guid.NewGuid()}/career-sheet");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Finalizing_makes_the_composed_career_sheet_immediately_available_and_owner_scoped()
    {
        var owner = await CreatePlayerAsync();
        var other = await CreatePlayerAsync();
        var characterId = await FinalizeRunnerAsync(owner, "Career Sheet Runner");

        var response = await owner.GetAsync($"/api/characters/{characterId}/career-sheet");
        response.EnsureSuccessStatusCode();
        var body = await ReadObjectAsync(response);
        Assert.Equal(characterId, body.GetProperty("characterId").GetGuid());
        Assert.Equal("human", body.GetProperty("sheet").GetProperty("metatype").GetProperty("id").GetString());
        Assert.True(body.GetProperty("currentKarma").GetInt32() >= 0);
        Assert.True(body.GetProperty("currentNuyen").GetInt32() > 0);
        Assert.Equal(0, body.GetProperty("lifetimeKarmaEarned").GetInt32());
        Assert.Equal(2, body.GetProperty("recentTransactions").GetArrayLength());
        Assert.Empty(body.GetProperty("recentAdvancements").EnumerateArray());
        Assert.Empty(body.GetProperty("acquiredInventory").EnumerateArray());
        Assert.Empty(body.GetProperty("nextActions").EnumerateArray());

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/characters/{characterId}/career-sheet")).StatusCode);
    }

    [Fact]
    public async Task Career_sheet_is_not_found_for_a_nonexistent_character()
    {
        var client = await CreatePlayerAsync();

        var response = await client.GetAsync($"/api/characters/{Guid.NewGuid()}/career-sheet");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpClient> CreatePlayerAsync()
    {
        var username = $"career-{Guid.NewGuid():N}";
        return await factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
    }

    private static async Task<Guid> FinalizeRunnerAsync(HttpClient client, string name)
    {
        var started = await client.PostAsJsonAsync("/api/character-creation/drafts", new
        {
            name,
            creationMethodId = "standard-priority",
        });
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var draft = await ReadObjectAsync(started);
        var characterId = draft.GetProperty("characterId").GetGuid();

        var updated = await client.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = draft.GetProperty("version").GetGuid(),
            name,
            document = ValidDocument(),
        });
        updated.EnsureSuccessStatusCode();
        var version = (await ReadObjectAsync(updated)).GetProperty("version").GetGuid();

        var finalized = await client.PostAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}/finalize", new { expectedVersion = version });
        finalized.EnsureSuccessStatusCode();

        return characterId;
    }

    // Mirrors CharacterCreationEndpointTests.ValidDocument — a genuinely
    // complete, ready-to-finalize document.
    private static object ValidDocument() => new
    {
        priorityAssignment = new
        {
            metatype = "e",
            attributes = "b",
            magicOrResonance = "a",
            skills = "c",
            resources = "d"
        },
        metatype = new { metatypeId = "human" },
        attributes = new
        {
            values = new Dictionary<string, int>
            {
                ["body"] = 3,
                ["agility"] = 3,
                ["reaction"] = 3,
                ["strength"] = 3,
                ["willpower"] = 3,
                ["logic"] = 3,
                ["intuition"] = 2,
                ["charisma"] = 0,
            }
        },
        specialAttributes = new
        {
            values = new Dictionary<string, int>
            {
                ["edge"] = 1,
                ["magic"] = 0,
                ["resonance"] = 0,
            }
        },
        nativeLanguages = new[] { new { name = "English" } },
        magicResonance = new
        {
            pathId = "magician",
            traditionId = "hermetic",
            skillGrants = new[] { new { skillId = "spellcasting" }, new { skillId = "summoning" } },
            spells = new[]
            {
                new { spellId = "manabolt", granted = true },
                new { spellId = "fireball", granted = true },
                new { spellId = "heal", granted = true },
                new { spellId = "detect-life", granted = true },
                new { spellId = "invisibility", granted = true },
                new { spellId = "armor", granted = true },
                new { spellId = "levitate", granted = true },
                new { spellId = "influence", granted = true },
                new { spellId = "combat-sense", granted = true },
                new { spellId = "increase-reflexes", granted = true },
            }
        },
        lifestyles = new[]
        {
            new { instanceId = "life-1", tierId = "street-lifestyle", isPrimary = true, prepaidMonths = 0 }
        }
    };

    private static async Task<JsonElement> ReadObjectAsync(HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.Clone();
    }
}
