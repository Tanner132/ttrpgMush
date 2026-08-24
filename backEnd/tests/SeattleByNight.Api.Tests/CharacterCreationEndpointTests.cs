using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SeattleByNight.Api.Tests;

public sealed class CharacterCreationEndpointTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";
    private readonly ApiTestFactory factory;

    public CharacterCreationEndpointTests(ApiTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task Catalogs_require_authentication_and_return_the_pinned_contract()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/character-creation/catalogs/current");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var client = await CreatePlayerAsync();
        var response = await client.GetAsync("/api/character-creation/catalogs/current?method=standard-priority");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("sr5-core", body.RootElement.GetProperty("rulesetId").GetString());
        Assert.Equal("1.0.0", body.RootElement.GetProperty("version").GetString());
        Assert.Equal(64, body.RootElement.GetProperty("semanticDigest").GetString()!.Length);
        Assert.Equal(2, body.RootElement.GetProperty("creationMethods").GetArrayLength());
        Assert.Equal(25, body.RootElement.GetProperty("priorityCells").GetArrayLength());
        Assert.Equal(17, body.RootElement.GetProperty("weaponAccessories").GetArrayLength());
        Assert.Equal(7, body.RootElement.GetProperty("armorModifications").GetArrayLength());
        Assert.Equal(3, body.RootElement.GetProperty("cyberlimbEnhancements").GetArrayLength());
        Assert.Equal(4, body.RootElement.GetProperty("vehicleModifications").GetArrayLength());
        Assert.Equal(6, body.RootElement.GetProperty("lifestyleTiers").GetArrayLength());
        Assert.Equal(5, body.RootElement.GetProperty("lifestyleOptions").GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/character-creation/catalogs/current?method=external-method")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/character-creation/catalogs/sr5-core/9.9.9")).StatusCode);
    }

    [Fact]
    public async Task Draft_mutations_require_antiforgery()
    {
        var client = await CreatePlayerAsync();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await client.PostAsJsonAsync("/api/character-creation/drafts", new
        {
            name = "No Token",
            creationMethodId = "standard-priority"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Draft_lifecycle_is_owner_scoped_versioned_and_server_authoritative()
    {
        var owner = await CreatePlayerAsync();
        var other = await CreatePlayerAsync();
        var started = await StartDraftAsync(owner, "API Runner");
        Assert.False(started.TryGetProperty("userId", out _));
        Assert.False(started.TryGetProperty("normalizedName", out _));
        Assert.Equal("priority.assignment.required",
            started.GetProperty("diagnostics")[0].GetProperty("code").GetString());

        var characterId = started.GetProperty("characterId").GetGuid();
        var version = started.GetProperty("version").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/character-creation/drafts/{characterId}")).StatusCode);

        var replacement = new
        {
            expectedVersion = version,
            name = "API Runner Renamed",
            document = ValidDocument(),
            userId = Guid.NewGuid(),
            isReadyToFinalize = false,
            calculatedTotal = int.MaxValue
        };
        var replacedResponse = await owner.PutAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}", replacement);
        replacedResponse.EnsureSuccessStatusCode();
        var replaced = await ReadObjectAsync(replacedResponse);
        Assert.True(replaced.GetProperty("isReadyToFinalize").GetBoolean());
        Assert.Empty(replaced.GetProperty("diagnostics").EnumerateArray());
        Assert.NotEqual(version, replaced.GetProperty("version").GetGuid());

        var stale = await owner.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", replacement);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleBody = await JsonDocument.ParseAsync(await stale.Content.ReadAsStreamAsync());
        Assert.Equal("character-creation.version-conflict", staleBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Change_preview_does_not_mutate_the_draft()
    {
        var client = await CreatePlayerAsync();
        var started = await StartDraftAsync(client, "Preview Runner");
        var characterId = started.GetProperty("characterId").GetGuid();
        var version = started.GetProperty("version").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}/change-preview",
            new { expectedVersion = version, document = ValidDocument() });
        response.EnsureSuccessStatusCode();
        var preview = await ReadObjectAsync(response);
        Assert.True(preview.GetProperty("candidate").GetProperty("isReadyToFinalize").GetBoolean());
        Assert.False(preview.GetProperty("requiresConfirmation").GetBoolean());

        var persisted = await client.GetFromJsonAsync<JsonElement>($"/api/character-creation/drafts/{characterId}");
        Assert.Equal(JsonValueKind.Null,
            persisted.GetProperty("document").GetProperty("priorityAssignment").ValueKind);
        Assert.Equal(version, persisted.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task Draft_list_and_discard_are_owner_scoped()
    {
        var owner = await CreatePlayerAsync();
        var other = await CreatePlayerAsync();
        var started = await StartDraftAsync(owner, "Discard Runner");
        var characterId = started.GetProperty("characterId").GetGuid();
        var version = started.GetProperty("version").GetGuid();

        var listed = await owner.GetFromJsonAsync<JsonElement>("/api/character-creation/drafts");
        Assert.Contains(listed.EnumerateArray(), item => item.GetProperty("characterId").GetGuid() == characterId);

        Assert.Equal(HttpStatusCode.NotFound,
            (await DeleteDraftAsync(other, characterId, version)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await DeleteDraftAsync(owner, characterId, version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.GetAsync($"/api/character-creation/drafts/{characterId}")).StatusCode);
    }

    [Fact]
    public async Task Finalization_reloads_rules_rejects_non_core_ids_and_returns_diagnostics()
    {
        var client = await CreatePlayerAsync();
        var started = await StartDraftAsync(client, "Invalid Runner");
        var characterId = started.GetProperty("characterId").GetGuid();
        var update = await client.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = started.GetProperty("version").GetGuid(),
            name = "Invalid Runner",
            document = new
            {
                priorityAssignment = new
                {
                    metatype = "run-faster-option",
                    attributes = "b",
                    magicOrResonance = "c",
                    skills = "d",
                    resources = "e"
                }
            }
        });
        update.EnsureSuccessStatusCode();
        var updated = await ReadObjectAsync(update);

        var finalized = await client.PostAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}/finalize",
            new { expectedVersion = updated.GetProperty("version").GetGuid() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, finalized.StatusCode);
        using var body = await JsonDocument.ParseAsync(await finalized.Content.ReadAsStreamAsync());
        Assert.Equal("character-creation.rule-violation", body.RootElement.GetProperty("code").GetString());
        Assert.Contains(body.RootElement.GetProperty("diagnostics").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "catalog.option.unknown");
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/character-creation/drafts/{characterId}")).StatusCode);
    }

    [Fact]
    public async Task Valid_draft_finalizes_once_and_exposes_only_the_owners_sheet()
    {
        var owner = await CreatePlayerAsync();
        var other = await CreatePlayerAsync();
        var started = await StartDraftAsync(owner, "Final API Runner");
        var characterId = started.GetProperty("characterId").GetGuid();
        var update = await owner.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = started.GetProperty("version").GetGuid(),
            name = "Final API Runner",
            document = ValidDocument()
        });
        var updated = await ReadObjectAsync(update);
        var version = updated.GetProperty("version").GetGuid();

        var finalize = await owner.PostAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}/finalize", new { expectedVersion = version });
        finalize.EnsureSuccessStatusCode();
        var sheet = await ReadObjectAsync(finalize);
        Assert.Equal("Evaluated", sheet.GetProperty("kind").GetString());
        Assert.Equal("standard-priority", sheet.GetProperty("sheet")
            .GetProperty("priorityAssignment").GetProperty("creationMethodId").GetString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.GetAsync($"/api/character-creation/drafts/{characterId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/characters/{characterId}/sheet")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/api/characters/{characterId}/sheet")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.PostAsJsonAsync($"/api/character-creation/drafts/{characterId}/finalize",
                new { expectedVersion = version })).StatusCode);
    }

    [Fact]
    public async Task Valid_sum_to_ten_draft_also_finalizes_into_the_new_character_room()
    {
        // ValidDocument's priority levels (e, b, a, c, d) cost 0+3+4+2+1 = 10,
        // so the same complete document is valid under both creation methods
        // — CHAR-811 requires both Standard Priority and Sum-to-Ten to reach
        // finalization, not just Standard Priority.
        var owner = await CreatePlayerAsync();
        var started = await StartDraftAsync(owner, "Sum To Ten Runner", "sum-to-ten");
        var characterId = started.GetProperty("characterId").GetGuid();
        var update = await owner.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = started.GetProperty("version").GetGuid(),
            name = "Sum To Ten Runner",
            document = ValidDocument()
        });
        var updated = await ReadObjectAsync(update);
        Assert.True(updated.GetProperty("isReadyToFinalize").GetBoolean(),
            string.Join("; ", updated.GetProperty("diagnostics").EnumerateArray().Select(item => item.GetProperty("code").GetString())));

        var finalize = await owner.PostAsJsonAsync(
            $"/api/character-creation/drafts/{characterId}/finalize",
            new { expectedVersion = updated.GetProperty("version").GetGuid() });
        finalize.EnsureSuccessStatusCode();
        var sheet = await ReadObjectAsync(finalize);
        Assert.Equal("sum-to-ten", sheet.GetProperty("sheet")
            .GetProperty("priorityAssignment").GetProperty("creationMethodId").GetString());
    }

    [Fact]
    public async Task Malformed_and_oversized_documents_are_rejected()
    {
        var client = await CreatePlayerAsync();
        var started = await StartDraftAsync(client, "Bounded Runner");
        var characterId = started.GetProperty("characterId").GetGuid();
        var version = started.GetProperty("version").GetGuid();

        var oversized = await client.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = version,
            name = "Bounded Runner",
            document = new
            {
                priorityAssignment = new
                {
                    metatype = new string('x', 65),
                    attributes = "b",
                    magicOrResonance = "c",
                    skills = "d",
                    resources = "e"
                }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        var invalidQualityRating = await client.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = version,
            name = "Bounded Runner",
            document = new
            {
                qualities = new[] { new { qualityId = "guts", rating = -1 } }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidQualityRating.StatusCode);

        var oversizedAttachmentCollection = await client.PutAsJsonAsync($"/api/character-creation/drafts/{characterId}", new
        {
            expectedVersion = version,
            name = "Bounded Runner",
            document = new
            {
                attachments = Enumerable.Range(0, 501).Select(index => new
                {
                    hostInstanceId = $"host-{index}",
                    accessoryId = "smartlink-implanted"
                })
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, oversizedAttachmentCollection.StatusCode);

        using var malformedContent = new StringContent("{", Encoding.UTF8, "application/json");
        var malformed = await client.PutAsync($"/api/character-creation/drafts/{characterId}", malformedContent);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    private async Task<HttpClient> CreatePlayerAsync()
    {
        var username = $"creator-{Guid.NewGuid():N}";
        return await factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
    }

    private static async Task<JsonElement> StartDraftAsync(HttpClient client, string name, string creationMethodId = "standard-priority")
    {
        var response = await client.PostAsJsonAsync("/api/character-creation/drafts", new
        {
            name,
            creationMethodId,
            userId = Guid.NewGuid(),
            lifecycleState = "Finalized"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadObjectAsync(response);
    }

    // A genuinely complete, ready-to-finalize document — CHAR-811 closed the
    // gap where metatype, attributes, skills/knowledge, magic-or-resonance,
    // and lifestyle were all silently optional at finalization.
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

    private static Task<HttpResponseMessage> DeleteDraftAsync(HttpClient client, Guid characterId, Guid version)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/character-creation/drafts/{characterId}")
        {
            Content = JsonContent.Create(new { expectedVersion = version })
        };
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadObjectAsync(HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.Clone();
    }
}
