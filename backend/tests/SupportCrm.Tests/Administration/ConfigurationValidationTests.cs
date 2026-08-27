using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Administration;

/// <summary>
/// <b>Invalid configuration fails fast at startup with a clear message</b> — the
/// <c>audit-configuration</c> intake's acceptance criterion, and docs/architecture.md §6.3.
///
/// <para>
/// Each test breaks one value deliberately and asserts <b>both</b> that startup fails <b>and that
/// the message names the offending value</b>. The second half matters as much as the first: a
/// startup failure saying only "configuration is invalid" costs more time than no validation.
/// </para>
///
/// <para>
/// <b>The six checks split across two mechanisms, and the tests follow the split.</b> Checks 2, 4,
/// 5 and 6 are structural, so <c>ValidateOnStart()</c> runs them while the host is being built —
/// those tests start a real host and assert it refuses to come up. Checks <b>1 and 3 read rows</b>,
/// so they run in <c>DatabaseInitializer</c> after migrations and seeding; those tests invoke
/// <see cref="ConfigurationValidator.ValidateAgainstDatabaseAsync"/> directly against a seeded
/// context.
/// </para>
/// <para>
/// <b>Why the referential pair is not driven through the host here:</b> this suite runs on SQLite so
/// it stays hermetic, and <c>DatabaseInitializer</c> begins with <c>MigrateAsync()</c> against a
/// SQL Server-flavoured migration (a filtered index, a named collation) that SQLite cannot apply.
/// The hosted path for these two is verified against <b>real SQL Server</b> by the story's
/// verification step 3 — the same division of labour the factory already documents for the
/// case-insensitive email collation.
/// </para>
/// </summary>
public sealed class ConfigurationValidationTests
{
    // ---------------------------------------------------------------------------------------
    // Checks 4 and 2 — structural, enforced while the host starts.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Plan test 8. A-6 fixes four levels; configuration exists so they have one home, <b>not</b> so
    /// a fifth can be added. A fifth level would silently break escalation ("raise one level") and
    /// the SLA target lookup.
    /// </summary>
    [Fact]
    public void A_fifth_priority_level_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Priorities:Levels:4"] = "Critical",
        });

        Assert.Contains(PriorityOptions.SectionName, message);
        Assert.Contains("Critical", message);
    }

    /// <summary>A rename is the same failure as an addition — the list must match exactly, in order.</summary>
    [Fact]
    public void A_renamed_priority_level_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Priorities:Levels:0"] = "Lowest",
        });

        Assert.Contains(PriorityOptions.SectionName, message);
        Assert.Contains("Lowest", message);
    }

    /// <summary>
    /// Plan test 6. A priority with no target is not a default-to-something situation: a ticket at
    /// that priority would carry no due date at all, and Story 09's breach sweep would never fire
    /// for it (A-3).
    /// </summary>
    [Fact]
    public void A_priority_with_no_SLA_target_stops_startup()
    {
        // Overwrite the Urgent row (index 3) with a level that is not a priority, so Urgent is left
        // with no target at all.
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Sla:Targets:Items:3:Priority"] = "Nonexistent",
        });

        Assert.Contains(SlaTargetOptions.SectionName, message);
        Assert.Contains("Urgent", message);
    }

    /// <summary>Check 2's other half — a zero or negative hour value is refused (A-3).</summary>
    [Fact]
    public void A_zero_hour_SLA_target_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Sla:Targets:Items:0:FirstResponseHours"] = "0",
        });

        Assert.Contains(SlaTargetOptions.SectionName, message);
    }

    /// <summary>
    /// Check 5. <b>Structural only</b>: an inverted range cannot render, so it fails — but this
    /// asserts nothing about which values are correct, because <b>OQ-1 is open</b>.
    /// </summary>
    [Fact]
    public void An_inverted_feedback_rating_scale_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Feedback:RatingScale:Min"] = "5",
            ["SupportCrm:Feedback:RatingScale:Max"] = "1",
        });

        Assert.Contains(FeedbackOptions.SectionName, message);
        Assert.Contains("OQ-1", message);
    }

    /// <summary>Check 6 — the attachment cap must be a positive number of bytes (T2-A).</summary>
    [Fact]
    public void A_zero_attachment_cap_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Attachments:MaxSizeBytes"] = "0",
        });

        Assert.Contains(AttachmentOptions.SectionName, message);
    }

    /// <summary>A category with no department mapping at all — A-14, caught before the row check.</summary>
    [Fact]
    public void A_category_with_no_department_mapping_stops_startup()
    {
        var message = AssertStartupFails(new()
        {
            ["SupportCrm:Categories:Items:0:DepartmentId"] = Guid.Empty.ToString(),
        });

        Assert.Contains(CategoryOptions.SectionName, message);
        Assert.Contains("billing", message);
    }

    // ---------------------------------------------------------------------------------------
    // Checks 1 and 3 — referential, enforced in DatabaseInitializer after seeding.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Plan test 5. <b>A-14: "an unmapped category is a configuration error and fails at startup
    /// validation."</b> The message names the category, because that is what an operator has to go
    /// and fix.
    /// </summary>
    [Fact]
    public async Task A_category_mapped_to_a_nonexistent_department_fails_the_database_check()
    {
        using var factory = new SupportCrmApiFactory();
        var branchId = await factory.EnsureBranchAsync("Validation Branch");

        var danglingDepartmentId = Guid.NewGuid();

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => factory.WithDbAsync(async db =>
        {
            await ConfigurationValidator.ValidateAgainstDatabaseAsync(
                db,
                new CategoryOptions
                {
                    Items =
                    [
                        new CategoryOption
                        {
                            Code = "orphaned",
                            Name = "Orphaned",
                            DepartmentId = danglingDepartmentId,
                        },
                    ],
                },
                new RegistrationOptions { DefaultBranchId = branchId },
                CancellationToken.None);

            return true;
        }));

        var message = string.Join(" ", failure.Failures);

        Assert.Contains("orphaned", message);
        Assert.Contains(danglingDepartmentId.ToString(), message);
        Assert.Contains("A-14", message);
    }

    /// <summary>
    /// Plan test 7. A-15 assigns this branch to every self-registering customer, so a dangling id
    /// would fail on the first registration rather than at startup.
    /// </summary>
    [Fact]
    public async Task A_default_branch_that_does_not_exist_fails_the_database_check()
    {
        using var factory = new SupportCrmApiFactory();
        var departmentId = await factory.EnsureDepartmentAsync("Validation Department");

        var danglingBranchId = Guid.NewGuid();

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => factory.WithDbAsync(async db =>
        {
            await ConfigurationValidator.ValidateAgainstDatabaseAsync(
                db,
                new CategoryOptions
                {
                    Items =
                    [
                        new CategoryOption { Code = "ok", Name = "Ok", DepartmentId = departmentId },
                    ],
                },
                new RegistrationOptions { DefaultBranchId = danglingBranchId },
                CancellationToken.None);

            return true;
        }));

        var message = string.Join(" ", failure.Failures);

        Assert.Contains("DefaultBranchId", message);
        Assert.Contains(danglingBranchId.ToString(), message);
        Assert.Contains("A-15", message);
    }

    /// <summary>The happy path: valid references pass, so the checks are not vacuously failing.</summary>
    [Fact]
    public async Task Valid_references_pass_the_database_check()
    {
        using var factory = new SupportCrmApiFactory();
        var departmentId = await factory.EnsureDepartmentAsync("Valid Department");
        var branchId = await factory.EnsureBranchAsync("Valid Branch");

        await factory.WithDbAsync(async db =>
        {
            await ConfigurationValidator.ValidateAgainstDatabaseAsync(
                db,
                new CategoryOptions
                {
                    Items =
                    [
                        new CategoryOption { Code = "billing", Name = "Billing", DepartmentId = departmentId },
                    ],
                },
                new RegistrationOptions { DefaultBranchId = branchId },
                CancellationToken.None);

            return true;
        });
    }

    /// <summary>
    /// Starts a real host with one value broken and returns the failure message.
    /// <para>
    /// <c>ValidateOnStart()</c> runs during host start, so the factory throws the moment the host is
    /// built — which is the behaviour being asserted: the application <b>does not come up</b>.
    /// </para>
    /// </summary>
    private static string AssertStartupFails(Dictionary<string, string?> overrides)
    {
        using var factory = new SupportCrmApiFactory { ConfigurationOverrides = overrides };

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        // OptionsValidationException may arrive wrapped, depending on where in host start it fires.
        var messages = new List<string>();
        for (var current = failure; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);

            if (current is OptionsValidationException validation)
            {
                messages.AddRange(validation.Failures);
            }
        }

        return string.Join(" | ", messages);
    }
}
