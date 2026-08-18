using System.Net;
using System.Net.Http.Json;
using System.Text;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class AdminAuthorizationTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public AdminAuthorizationTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchUsers_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchUsers_NonAdmin_ReturnsForbidden()
    {
        var client = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SearchUsers_Admin_ReturnsUsersWithRoles()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.GetAsync("/api/admin/users?query=devuser");
        response.EnsureSuccessStatusCode();

        var users = (await response.Content.ReadFromJsonAsync<List<AdminUserDto>>())!;
        var dev = Assert.Single(users);

        Assert.Equal(DevelopmentDataSeeder.DevUserId, dev.Id);
        Assert.Contains("Administrator", dev.Roles);
    }

    [Fact]
    public async Task Registration_NewUser_ReceivesNoRoles()
    {
        var client = await _factory.RegisterAndLoginAsync($"fresh-{Guid.NewGuid():N}", $"fresh-{Guid.NewGuid():N}@test.local", Password);

        var response = await client.GetAsync("/api/account/me");
        response.EnsureSuccessStatusCode();

        var account = (await response.Content.ReadFromJsonAsync<AccountWithRolesDto>())!;
        Assert.Empty(account.Roles);
    }

    [Fact]
    public async Task AssignRole_Admin_AssignsAndAudits()
    {
        var target = await _factory.RegisterAndLoginAsync($"assign-{Guid.NewGuid():N}", $"assign-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Moderator" });
        response.EnsureSuccessStatusCode();

        var search = await admin.GetAsync($"/api/admin/users?query={targetAccount.UserName}");
        var users = (await search.Content.ReadFromJsonAsync<List<AdminUserDto>>())!;
        var match = Assert.Single(users);
        Assert.Contains("Moderator", match.Roles);

        var audit = await ReadAuditLogAsync(admin, "RoleAssigned");
        Assert.Contains(audit.Entries, e => e.TargetId == targetAccount.Id && e.Action == "RoleAssigned");
    }

    [Fact]
    public async Task AssignRole_NonAdmin_ReturnsForbidden()
    {
        var client = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);

        var response = await client.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/roles", new { roleName = "Moderator" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_UnknownRole_ReturnsBadRequest()
    {
        var target = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Superuser" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_UnknownUser_ReturnsNotFound()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/roles", new { roleName = "Moderator" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_ReturnsConflict()
    {
        var target = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        var assign = await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Moderator" });
        assign.EnsureSuccessStatusCode();

        var duplicate = await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Moderator" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_Admin_RemovesAndAudits()
    {
        var target = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Moderator" });

        var response = await admin.DeleteAsync($"/api/admin/users/{targetAccount.Id}/roles/Moderator");
        response.EnsureSuccessStatusCode();

        var search = await admin.GetAsync($"/api/admin/users?query={targetAccount.UserName}");
        var users = (await search.Content.ReadFromJsonAsync<List<AdminUserDto>>())!;
        var match = Assert.Single(users);
        Assert.DoesNotContain("Moderator", match.Roles);

        var audit = await ReadAuditLogAsync(admin, "RoleRemoved");
        Assert.Contains(audit.Entries, e => e.TargetId == targetAccount.Id && e.Action == "RoleRemoved");
    }

    [Fact]
    public async Task RemoveRole_NotAssigned_ReturnsConflict()
    {
        var target = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.DeleteAsync($"/api/admin/users/{targetAccount.Id}/roles/Moderator");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_NonAdmin_ReturnsForbidden()
    {
        var client = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);

        var response = await client.GetAsync("/api/admin/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_Admin_ReturnsNewestFirstWithFilters()
    {
        var target = await _factory.RegisterAndLoginAsync($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@test.local", Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "Moderator" });
        await admin.PostAsJsonAsync($"/api/admin/users/{targetAccount.Id}/roles", new { roleName = "WorldBuilder" });

        var page = await ReadAuditLogAsync(admin, "RoleAssigned");

        Assert.NotNull(page);
        var targetEntries = page.Entries.Where(e => e.TargetId == targetAccount.Id).ToList();
        Assert.Equal(2, targetEntries.Count);

        // Newest first by descending timestamp.
        Assert.True(targetEntries[0].CreatedAtUtc >= targetEntries[1].CreatedAtUtc);
    }

    [Fact]
    public async Task AuditLog_InvalidFilters_ReturnBadRequest()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var oversizedAction = await admin.GetAsync($"/api/admin/audit?action={new string('x', 101)}");
        var malformedCursor = await admin.GetAsync("/api/admin/audit?cursor=not-a-cursor");
        var outOfRangeCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "9223372036854775807|00000000000000000000000000000000"));
        var outOfRange = await admin.GetAsync($"/api/admin/audit?cursor={Uri.EscapeDataString(outOfRangeCursor)}");
        var reversedRange = await admin.GetAsync(
            "/api/admin/audit?from=2026-08-18T12:00:00Z&to=2026-08-18T11:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, oversizedAction.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reversedRange.StatusCode);
    }

    [Fact]
    public async Task RoleAssignment_InvalidatesTargetUsersExistingCookie()
    {
        var target = await _factory.RegisterAndLoginAsync(
            $"user-{Guid.NewGuid():N}",
            $"user-{Guid.NewGuid():N}@test.local",
            Password);
        var targetAccount = await _factory.GetAccountAsync(target);
        var admin = await _factory.LoginDevAdminAsync();

        var assignment = await admin.PostAsJsonAsync(
            $"/api/admin/users/{targetAccount.Id}/roles",
            new { roleName = "Moderator" });
        var staleCookieRequest = await target.GetAsync("/api/account/me");

        Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staleCookieRequest.StatusCode);
    }

    private static async Task<AuditLogPageDto> ReadAuditLogAsync(HttpClient admin, string? action = null)
    {
        var url = action is null ? "/api/admin/audit" : $"/api/admin/audit?action={Uri.EscapeDataString(action)}";
        var response = await admin.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuditLogPageDto>())!;
    }

    private sealed record AdminUserDto(Guid Id, string UserName, string Email, IReadOnlyList<string> Roles);

    private sealed record AuditLogEntryDto(
        Guid Id,
        DateTimeOffset CreatedAtUtc,
        Guid ActorUserId,
        string? ActorUserName,
        string Action,
        string TargetType,
        Guid TargetId,
        string? Details);

    private sealed record AuditLogPageDto(IReadOnlyList<AuditLogEntryDto> Entries, string? NextCursor);
}
