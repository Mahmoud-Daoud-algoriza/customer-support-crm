using Microsoft.EntityFrameworkCore;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Tests.Administration;

/// <summary>
/// <b>Configured concepts are configuration, not tables</b> — docs/data-model.md §2.16 lists
/// <c>Category</c>, <c>Priority</c>, <c>SlaPolicy</c>, <c>QuickReply</c>, <c>Branding</c> and
/// <c>Setting</c> as explicitly not entities, each with its reason.
///
/// <para>
/// <b>This is the test that keeps T2-I true as later stories add tables.</b> The pressure is real:
/// Story 05 needs categories and priorities, Story 08 needs quick replies, Story 09 needs SLA
/// targets — and the obvious way to make any of them editable is a table plus a screen. T2-I says
/// changing configuration is a <b>redeploy</b>. A story that adds one of these tables breaks this
/// test, which is the intended alarm rather than an inconvenience: if the decision is genuinely to
/// be revisited, it is revisited in <c>product-scope.md</c> first, not in a migration.
/// </para>
/// </summary>
public sealed class NoConfigurationEntityTests
{
    /// <summary>
    /// Plan test 9. Asserted by reflecting over the context rather than by reading the migration:
    /// the model is the thing that would grow a set, and a migration can be regenerated.
    /// </summary>
    [Theory]
    [InlineData("Category")]
    [InlineData("Categories")]
    [InlineData("Priority")]
    [InlineData("Priorities")]
    [InlineData("SlaPolicy")]
    [InlineData("SlaPolicies")]
    [InlineData("QuickReply")]
    [InlineData("QuickReplies")]
    [InlineData("Branding")]
    [InlineData("Setting")]
    [InlineData("Settings")]
    public void No_DbSet_exists_for_a_configured_concept(string forbidden)
    {
        var sets = DbSetNames();

        Assert.DoesNotContain(forbidden, sets, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The positive half, so the test above cannot pass by reflecting over nothing: the sets that
    /// <em>should</em> exist after Stories 02 and 03 are present.
    /// </summary>
    [Fact]
    public void The_entity_sets_that_should_exist_do_exist()
    {
        var sets = DbSetNames();

        Assert.Contains("Users", sets);
        Assert.Contains("AuditEntries", sets);
        Assert.Contains("Departments", sets);
        Assert.Contains("Branches", sets);
    }

    /// <summary>
    /// <b>Tenancy has no home either</b> (A-2, T3-G, data-model §2.16). Checked in the same place
    /// because it is the same class of decision: a single organization, with no tenant concept.
    /// </summary>
    [Theory]
    [InlineData("Tenant")]
    [InlineData("Tenants")]
    [InlineData("Organization")]
    [InlineData("Organizations")]
    [InlineData("Role")]
    [InlineData("Roles")]
    public void No_DbSet_exists_for_a_concept_the_model_excludes(string forbidden)
    {
        var sets = DbSetNames();

        Assert.DoesNotContain(forbidden, sets, StringComparer.OrdinalIgnoreCase);
    }

    private static string[] DbSetNames() => typeof(SupportCrmDbContext)
        .GetProperties()
        .Where(p => p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .Select(p => p.Name)
        .ToArray();
}
