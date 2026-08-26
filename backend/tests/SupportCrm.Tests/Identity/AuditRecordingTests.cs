using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Identity;

/// <summary>
/// Sign-in and user-administration actions must be recorded through the <b>single</b> audit recorder
/// (docs/architecture.md §2.4), with the actor attribution docs/data-model.md §2.14 specifies.
/// </summary>
public sealed class AuditRecordingTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    private Task<List<AuditEntry>> EntriesForAsync(string action) => factory.WithDbAsync(db =>
        db.AuditEntries.AsNoTracking().Where(a => a.Action == action).ToListAsync());

    /// <summary>
    /// <c>actorUserId</c> may be null for exactly one reason — "no user could be resolved, a failed
    /// sign-in" (§2.14). A <b>successful</b> sign-in resolved a user, so it must be attributed.
    /// </summary>
    [Fact]
    public async Task A_successful_sign_in_is_attributed_to_the_user_who_signed_in()
    {
        const string email = "audit.success@test.local";
        const string password = "Audit3dPassw0rd!";

        var userId = await factory.AddStaffUserAsync(UserRole.Agent, email, password);

        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var entry = Assert.Single(
            await EntriesForAsync(AuditAction.SignInSucceeded), a => a.TargetId == userId);

        Assert.Equal(AuditOutcome.Success, entry.Outcome);
        Assert.Equal(userId, entry.ActorUserId);
        Assert.Equal(AuditTargetType.User, entry.TargetType);

        // Used only when the actor could not be resolved, which is not this case.
        Assert.Null(entry.ActorDescriptor);
    }

    /// <summary>
    /// A failed sign-in has no resolvable actor, so it carries the submitted identifier instead —
    /// otherwise the attempt would be unattributable, which is what §2.14 exists to prevent.
    /// </summary>
    [Fact]
    public async Task A_failed_sign_in_records_the_submitted_identifier_and_no_actor()
    {
        const string email = "audit.failure@test.local";

        await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassw0rd!" });

        var entry = Assert.Single(
            await EntriesForAsync(AuditAction.SignInFailed), a => a.ActorDescriptor == email);

        Assert.Equal(AuditOutcome.Failure, entry.Outcome);
        Assert.Null(entry.ActorUserId);
    }

    /// <summary>
    /// An over-long submitted identifier must be <b>truncated, not rejected</b>
    /// (docs/data-model.md §6.1): recording the attempt is the whole point of the column, so an
    /// absurd input must not be able to suppress its own audit entry.
    /// </summary>
    [Fact]
    public async Task An_over_long_submitted_identifier_is_truncated_rather_than_dropped()
    {
        var absurd = new string('x', 400) + "@test.local";

        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email = absurd, password = "irrelevant" });

        // The attempt is still refused the same way as any other.
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        // And it is still recorded, at the column's width.
        var entry = Assert.Single(
            await EntriesForAsync(AuditAction.SignInFailed),
            a => a.ActorDescriptor is { Length: 256 } d && d.StartsWith("xxx"));

        Assert.Equal(256, entry.ActorDescriptor!.Length);
        Assert.Null(entry.ActorUserId);
    }

    [Fact]
    public async Task User_administration_actions_are_recorded_with_the_administrator_as_actor()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "audit.admin@test.local");
        var client = factory.CreateClientFor(adminId);
        var departmentId = await factory.EnsureDepartmentAsync("Audit Department");
        var otherDepartmentId = await factory.EnsureDepartmentAsync("Audit Department Two");

        var created = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "audit.subject@test.local",
            password = "Passw0rd!",
            displayName = "Audit Subject",
            role = "Agent",
            departmentId,
        });
        created.EnsureSuccessStatusCode();

        var subjectId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetGuid();

        (await client.PatchAsJsonAsync($"/api/v1/users/{subjectId}", new { role = "Manager" }))
            .EnsureSuccessStatusCode();

        (await client.PatchAsJsonAsync($"/api/v1/users/{subjectId}", new { departmentId = otherDepartmentId }))
            .EnsureSuccessStatusCode();

        (await client.PostAsync($"/api/v1/users/{subjectId}/deactivate", content: null))
            .EnsureSuccessStatusCode();

        foreach (var action in new[]
                 {
                     AuditAction.UserCreated, AuditAction.UserRoleChanged,
                     AuditAction.UserDepartmentChanged, AuditAction.UserDeactivated,
                 })
        {
            var entry = Assert.Single(await EntriesForAsync(action), a => a.TargetId == subjectId);

            Assert.Equal(adminId, entry.ActorUserId);
            Assert.Equal(AuditTargetType.User, entry.TargetType);
            Assert.Equal(AuditOutcome.Success, entry.Outcome);
        }
    }

    /// <summary>
    /// Append-only by construction (docs/architecture.md §2.4): the entity exposes no mutator at all,
    /// so there is no update or delete path that a later story could expose by accident.
    /// </summary>
    [Fact]
    public void AuditEntry_exposes_no_mutator()
    {
        var settable = typeof(AuditEntry)
            .GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(settable);

        var mutators = typeof(AuditEntry)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == typeof(AuditEntry))
            .Select(m => m.Name)
            .ToArray();

        Assert.Empty(mutators);
    }
}
