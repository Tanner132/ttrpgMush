using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Api.Endpoints;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Api.Tests;

// Milestone 7 step 3: the World Forge's server surface. The builder is a
// privileged tool over live content, so the tests that matter are the ones
// about who may write, what a write can do to a running game, and whether the
// publish gate can be talked past.
public sealed class GameContentEndpointTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";
    private const string WarehouseMission = "gang-warehouse-retrieval";

    private readonly ApiTestFactory _factory;

    public GameContentEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Inventory_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/content");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_Player_ReturnsForbidden()
    {
        var username = $"content-player-{Guid.NewGuid():N}";
        var player = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);

        var response = await player.GetAsync("/api/admin/content");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveDraft_Player_ReturnsForbidden()
    {
        var username = $"content-writer-{Guid.NewGuid():N}";
        var player = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);

        var response = await player.PutAsJsonAsync(
            $"/api/admin/content/Test/{WarehouseMission}", new { json = "{}" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_ListsTheSeededBundleAsPublishedAndClean()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var inventory = await GetInventoryAsync(admin);

        Assert.Null(inventory.CorpusError);
        Assert.NotEmpty(inventory.Revision);
        var mission = inventory.Definitions.Single(definition =>
            definition.Kind == GameContentKind.Mission && definition.ContentKey == WarehouseMission);
        Assert.Equal(GameContentStatus.Published, mission.Status);
        Assert.False(mission.HasPendingEdits);
        Assert.Null(mission.DraftError);
        // Every kind the composer knows about came through the import.
        Assert.Contains(inventory.Definitions, definition => definition.Kind == GameContentKind.Encounter);
        Assert.Contains(inventory.Definitions, definition => definition.Kind == GameContentKind.Scene);
        Assert.Contains(inventory.Definitions, definition => definition.Kind == GameContentKind.Test);
    }

    [Fact]
    public async Task Palette_ExposesTheEngineOwnedVocabulary()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var palette = await admin.GetFromJsonAsync<GameContentPaletteResponse>(
            "/api/admin/content/palette", ApiTestFactory.JsonOptions);

        Assert.NotNull(palette);
        Assert.Contains(palette.Attributes, option => option.Id == "intuition");
        Assert.Contains(palette.Skills, skill => skill.Id == "unarmed-combat" && skill.LinkedAttribute == "agility");
        // Extended tests are in the enum but the resolver refuses them, so the
        // builder must never offer them.
        Assert.DoesNotContain(palette.TestKinds, option => option.Id == "Extended");
        // The ids an authored test may not shadow.
        Assert.Contains(palette.BuiltInTests, option => option.Id == "sneak-past");

        // Enum members cross the wire spelled the way an authored fragment
        // spells them, so an editor can write them straight into the JSON.
        Assert.Contains(palette.NpcPools, option => option.Id == "sneaking");
        Assert.Contains(palette.NpcAwareness, option => option.Id == "pacified");
        Assert.Contains(palette.DamageTypes, option => option.Id == "physical");
        Assert.Contains(palette.FiringModes, option => option.Id == "semiAutomatic");
        Assert.Contains(palette.ObjectiveKinds, option => option.Id == "deliverItem");
        Assert.Contains(palette.RepeatabilityKinds, option => option.Id == "cooldown");
    }

    [Fact]
    public async Task SaveDraft_WhosePayloadIdDisagreesWithTheRoute_IsRefused()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.PutAsJsonAsync(
            "/api/admin/content/Test/some-other-key",
            new { json = TestJson("payload-key", threshold: 2) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("payload-key", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveDraft_ThenPublish_PutsAuthoredContentIntoPlayAndIsAudited()
    {
        var admin = await _factory.LoginDevAdminAsync();
        const string key = "builder-authored-test";

        var saved = await admin.PutAsJsonAsync(
            $"/api/admin/content/Test/{key}", new { json = TestJson(key, threshold: 2) });
        saved.EnsureSuccessStatusCode();

        var detail = (await saved.Content.ReadFromJsonAsync<GameContentDetailResponse>(
            ApiTestFactory.JsonOptions))!;
        Assert.Equal(GameContentStatus.Draft, detail.Summary.Status);
        Assert.True(detail.Summary.HasPendingEdits);
        Assert.Null(detail.PublishedJson);

        // A draft is invisible to the game until it is published.
        var beforePublish = await GetInventoryAsync(admin);
        var draft = beforePublish.Definitions.Single(definition => definition.ContentKey == key);
        Assert.Equal(GameContentStatus.Draft, draft.Status);
        Assert.Null(draft.DraftError);

        var published = await PublishAsync(admin, key);
        Assert.True(published.IsValid, published.Error);

        var afterPublish = await GetInventoryAsync(admin);
        var live = afterPublish.Definitions.Single(definition => definition.ContentKey == key);
        Assert.Equal(GameContentStatus.Published, live.Status);
        Assert.False(live.HasPendingEdits);
        // Publishing re-stamps the revision the game is serving.
        Assert.NotEqual(beforePublish.Revision, afterPublish.Revision);

        await using var db = _factory.CreateDbContext();
        var audited = await db.AuditRecords.AsNoTracking()
            .Where(record => record.TargetType == AuditTargetTypes.GameContent)
            .Select(record => record.Action)
            .ToListAsync();
        Assert.Contains(AuditActions.GameContentDraftSaved, audited);
        Assert.Contains(AuditActions.GameContentPublished, audited);
    }

    [Fact]
    public async Task Publish_RefusesADraftTheLoaderRejects_AndLeavesItADraft()
    {
        var admin = await _factory.LoginDevAdminAsync();
        const string key = "builder-broken-test";

        // A threshold test with no threshold: the loader's own rule, enforced
        // at publish exactly as it is at startup.
        var saved = await admin.PutAsJsonAsync(
            $"/api/admin/content/Test/{key}", new { json = TestJson(key, threshold: null) });
        saved.EnsureSuccessStatusCode();

        // The inventory says up front that this one cannot be published, with
        // the loader's own message.
        var inventory = await GetInventoryAsync(admin);
        var blocked = inventory.Definitions.Single(definition => definition.ContentKey == key);
        Assert.Contains("must declare a positive threshold", blocked.DraftError);

        var result = await PublishAsync(admin, key);
        Assert.False(result.IsValid);
        Assert.Contains("must declare a positive threshold", result.Error);

        var detail = await admin.GetFromJsonAsync<GameContentDetailResponse>(
            $"/api/admin/content/Test/{key}", ApiTestFactory.JsonOptions);
        Assert.Equal(GameContentStatus.Draft, detail!.Summary.Status);
        Assert.Null(detail.PublishedJson);
    }

    [Fact]
    public async Task Publish_RefusesATestThatShadowsABuiltInOne()
    {
        var admin = await _factory.LoginDevAdminAsync();
        const string key = "sneak-past";

        var saved = await admin.PutAsJsonAsync(
            $"/api/admin/content/Test/{key}", new { json = TestJson(key, threshold: 2) });
        saved.EnsureSuccessStatusCode();

        var result = await PublishAsync(admin, key);

        Assert.False(result.IsValid);
        Assert.Contains("shadows a built-in development test", result.Error);
    }

    [Fact]
    public async Task Retire_TakesContentOutOfPlayAndIsAudited()
    {
        var admin = await _factory.LoginDevAdminAsync();
        const string key = "builder-retirable-test";

        (await admin.PutAsJsonAsync(
            $"/api/admin/content/Test/{key}", new { json = TestJson(key, threshold: 2) }))
            .EnsureSuccessStatusCode();
        Assert.True((await PublishAsync(admin, key)).IsValid);

        var response = await admin.PostAsync($"/api/admin/content/Test/{key}/retire", content: null);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<GameContentValidationResponse>(
            ApiTestFactory.JsonOptions))!;
        Assert.True(result.IsValid, result.Error);

        var inventory = await GetInventoryAsync(admin);
        var retired = inventory.Definitions.Single(definition => definition.ContentKey == key);
        Assert.Equal(GameContentStatus.Retired, retired.Status);
        // Retiring is reversible: the definition still carries edits to publish.
        Assert.True(retired.HasPendingEdits);
        // And the corpus it is still part of remains loadable.
        Assert.Null(inventory.CorpusError);

        await using var db = _factory.CreateDbContext();
        Assert.True(await db.AuditRecords.AnyAsync(
            record => record.Action == AuditActions.GameContentRetired));
    }

    [Fact]
    public async Task Delete_RefusesContentTheCorpusStillPointsAt_AndSaysWhy()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var check = await admin.GetFromJsonAsync<GameContentDeletableResponse>(
            "/api/admin/content/NpcTemplate/street-ganger/deletable", ApiTestFactory.JsonOptions);
        Assert.False(check!.CanDelete);
        Assert.Contains("street-ganger", check.Reason);

        var response = await admin.DeleteAsync("/api/admin/content/NpcTemplate/street-ganger");
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<GameContentValidationResponse>(
            ApiTestFactory.JsonOptions))!;

        Assert.False(result.IsValid);
        await using var db = _factory.CreateDbContext();
        Assert.True(await db.GameContentDefinitions.AnyAsync(row => row.ContentKey == "street-ganger"));
    }

    [Fact]
    public async Task Delete_RemovesADraftNothingHasEverSeen()
    {
        var admin = await _factory.LoginDevAdminAsync();
        const string key = "builder-throwaway-test";

        (await admin.PutAsJsonAsync(
            $"/api/admin/content/Test/{key}", new { json = TestJson(key, threshold: 2) }))
            .EnsureSuccessStatusCode();

        var check = await admin.GetFromJsonAsync<GameContentDeletableResponse>(
            $"/api/admin/content/Test/{key}/deletable", ApiTestFactory.JsonOptions);
        Assert.True(check!.CanDelete);

        var response = await admin.DeleteAsync($"/api/admin/content/Test/{key}");
        response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<GameContentValidationResponse>(
            ApiTestFactory.JsonOptions))!.IsValid);

        await using var db = _factory.CreateDbContext();
        Assert.False(await db.GameContentDefinitions.AnyAsync(row => row.ContentKey == key));
        Assert.True(await db.AuditRecords.AnyAsync(
            record => record.Action == AuditActions.GameContentDeleted));
    }

    [Fact]
    public async Task Retire_Player_ReturnsForbidden()
    {
        var username = $"content-retirer-{Guid.NewGuid():N}";
        var player = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);

        var response = await player.PostAsync(
            $"/api/admin/content/Mission/{WarehouseMission}/retire", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDefinition_WithAnUnknownKind_IsRefused()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.GetAsync($"/api/admin/content/Dialogue/{WarehouseMission}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<GameContentInventoryResponse> GetInventoryAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<GameContentInventoryResponse>(
            "/api/admin/content", ApiTestFactory.JsonOptions))!;

    private static async Task<GameContentValidationResponse> PublishAsync(HttpClient client, string key)
    {
        var response = await client.PostAsync($"/api/admin/content/Test/{key}/publish", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GameContentValidationResponse>(
            ApiTestFactory.JsonOptions))!;
    }

    private static string TestJson(string id, int? threshold)
    {
        var thresholdLine = threshold is null ? string.Empty : $"\"threshold\": {threshold},";
        return $$"""
            {
              "id": "{{id}}",
              "displayName": "Authored Test",
              "description": "Logic + Computer [Mental] — authored in the builder.",
              "kind": "threshold",
              "limit": "mental",
              {{thresholdLine}}
              "pool": [
                { "kind": "attribute", "id": "logic" },
                { "kind": "skill", "id": "computer" }
              ],
              "tags": ["mental"]
            }
            """;
    }
}
