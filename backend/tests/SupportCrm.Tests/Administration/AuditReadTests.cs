using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Administration;

/// <summary>
/// <c>GET /audit</c> — Story 16 Part B's read surface (docs/architecture.md §2.4,
/// docs/api-design.md §5.12, §6.9).
/// <para>
/// <c>AuditRecordingTests</c> (Story 02) already proves the write path — every action is recorded,
/// with the right actor attribution. These tests prove the read endpoint on top of it: role gating,
/// filtering, the null-actor projection, and that no write route exists.
/// </para>
/// </summary>
public sealed class AuditReadTests(SupportCrmApiFactory factory) : IClassFixture<SupportCrmApiFactory>
{
    private const string Audit = "/api/v1/audit";

    /// <summary>Plan test 10, the staff half.</summary>
    [Theory]
    [InlineData(nameof(UserRole.Administrator), HttpStatusCode.OK)]
    [InlineData(nameof(UserRole.Manager), HttpStatusCode.Forbidden)]
    [InlineData(nameof(UserRole.Agent), HttpStatusCode.Forbidden)]
    public async Task Only_an_Administrator_may_read_the_audit_log(string roleName, HttpStatusCode expected)
    {
        var role = Enum.Parse<UserRole>(roleName);
        var userId = await factory.AddStaffUserAsync(role, $"audit.role.{roleName.ToLowerInvariant()}@test.local");

        var response = await factory.CreateClientFor(userId).GetAsync(Audit);

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Plan test 10, the Customer half — a different fixture helper produces that role.</summary>
    [Fact]
    public async Task A_Customer_is_refused_the_audit_log()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("audit.role.customer@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync(Audit);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Plan test 11 — <c>actorUserId</c>, <c>action</c> and a <c>from</c>/<c>to</c> range, combined
    /// with AND (docs/api-design.md §2.1, §5.12).
    /// <para>
    /// The rows are seeded directly, with timestamps a live write path cannot promise (the recorder
    /// always stamps "now") — establishing that precondition, not the audited action itself, is what
    /// this test needs. <c>AuditRecordingTests</c> already proves the write path.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Filters_combine_by_actor_action_and_date_range()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "audit.filter.admin@test.local");
        var subjectId = await factory.AddStaffUserAsync(UserRole.Agent, "audit.filter.subject@test.local");
        var otherId = await factory.AddStaffUserAsync(UserRole.Agent, "audit.filter.other@test.local");

        var now = DateTimeOffset.UtcNow;

        await factory.WithDbAsync(async db =>
        {
            db.AuditEntries.AddRange(
                AuditEntry.Record(
                    Guid.NewGuid(), now.AddDays(-10), AuditAction.UserRoleChanged, AuditOutcome.Success,
                    subjectId, null, AuditTargetType.User, subjectId),
                AuditEntry.Record(
                    Guid.NewGuid(), now, AuditAction.UserRoleChanged, AuditOutcome.Success,
                    subjectId, null, AuditTargetType.User, subjectId),
                AuditEntry.Record(
                    Guid.NewGuid(), now, AuditAction.UserDeactivated, AuditOutcome.Success,
                    subjectId, null, AuditTargetType.User, subjectId),
                AuditEntry.Record(
                    Guid.NewGuid(), now, AuditAction.UserRoleChanged, AuditOutcome.Success,
                    otherId, null, AuditTargetType.User, otherId));

            return await db.SaveChangesAsync();
        });

        var client = factory.CreateClientFor(adminId);

        var byActor = await ReadItemsAsync(client, $"{Audit}?actorUserId={subjectId}");
        Assert.Equal(3, byActor.Length);
        Assert.All(byActor, e => Assert.Equal(subjectId, e.GetProperty("actor").GetProperty("id").GetGuid()));

        var byAction = await ReadItemsAsync(client, $"{Audit}?action={AuditAction.UserRoleChanged}");
        Assert.Equal(3, byAction.Length);
        Assert.All(byAction, e => Assert.Equal(AuditAction.UserRoleChanged, e.GetProperty("action").GetString()));

        var combined = await ReadItemsAsync(
            client,
            $"{Audit}?actorUserId={subjectId}&action={AuditAction.UserRoleChanged}" +
            $"&from={Uri.EscapeDataString(now.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(now.AddDays(1).ToString("O"))}");

        Assert.Single(combined);
    }

    /// <summary>
    /// Plan test 12. <c>actor</c> is omitted from the JSON rather than serialized as <c>null</c>
    /// (Program.cs, <c>WhenWritingNull</c>) — the same shape <c>TicketHistoryTests</c> relies on for
    /// <c>oldValue</c>/<c>newValue</c>.
    /// </summary>
    [Fact]
    public async Task A_failed_sign_in_appears_with_no_actor_and_the_submitted_identifier()
    {
        var adminId = await factory.AddStaffUserAsync(
            UserRole.Administrator, "audit.failedsignin.admin@test.local");
        const string email = "audit.failedsignin.subject@test.local";

        (await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassw0rd!" }))
            .Dispose();

        var entries = await ReadItemsAsync(
            factory.CreateClientFor(adminId), $"{Audit}?action={AuditAction.SignInFailed}");

        var entry = Assert.Single(entries, e => e.GetProperty("actorDescriptor").GetString() == email);

        Assert.False(entry.TryGetProperty("actor", out _));
        Assert.Equal("Failure", entry.GetProperty("outcome").GetString());
    }

    /// <summary>Plan test 13.</summary>
    [Fact]
    public async Task No_route_accepts_a_write_to_audit()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "audit.nowrite.admin@test.local");
        var client = factory.CreateClientFor(adminId);

        var post = await client.PostAsJsonAsync(Audit, new { invented = true });
        var patch = await client.PatchAsJsonAsync(Audit, new { invented = true });
        var put = await client.PutAsJsonAsync(Audit, new { invented = true });
        var delete = await client.DeleteAsync(Audit);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patch.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    /// <summary>
    /// Plan test 14, the reflection half. <c>AuditRecordingTests.AuditEntry_exposes_no_mutator</c>
    /// already proves the entity itself has no mutator; this proves no <b>service</b> in the module
    /// exposes one either (docs/architecture.md §2.4).
    /// </summary>
    [Fact]
    public void No_service_in_Administration_exposes_an_update_or_delete_method()
    {
        var offending = typeof(AuditQueryService).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "SupportCrm.Application.Modules.Administration")
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m =>
                m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToArray();

        Assert.Empty(offending);
    }

    /// <summary>
    /// Plan test 14, the other half — <b>no source file removes an <c>AuditEntry</c></b>, the same
    /// proof <c>TicketHistoryTests.No_source_path_removes_a_ticket_activity</c> gives its table. A
    /// reflection test cannot see a <c>Remove</c> call; this reads the source the way a reviewer
    /// would.
    /// </summary>
    [Fact]
    public void No_source_path_removes_an_audit_entry()
    {
        var root = SourceRoot();

        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return text.Contains("AuditEntries.Remove", StringComparison.Ordinal)
                    || text.Contains("AuditEntries.RemoveRange", StringComparison.Ordinal)
                    || text.Contains("AuditEntries.ExecuteDelete", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(offenders);
    }

    private static async Task<JsonElement[]> ReadItemsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. body.GetProperty("items").EnumerateArray()];
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory!.FullName, "src");
    }
}
