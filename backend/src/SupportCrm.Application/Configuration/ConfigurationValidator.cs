using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// Startup validation for Story 16 Part A — the six checks the plan enumerates.
///
/// <para>
/// <b>Why it exists.</b> The <c>audit-configuration</c> intake requires that *"invalid configuration
/// fails fast at startup with a clear message"* rather than degrading silently at runtime
/// (docs/architecture.md §6.3). A category with no department is not a runtime inconvenience: A-14
/// derives a ticket's department from its category, so the failure would surface as the first
/// customer-submitted ticket being unroutable, long after deployment.
/// </para>
///
/// <para>
/// <b>Two kinds of check, two places.</b> Checks 2, 4, 5 and 6 are structural and live in the
/// <see cref="IValidateOptions{TOptions}"/> implementations below, which
/// <c>ValidateOnStart()</c> runs while the host is being built. Checks <b>1 and 3 read the
/// database</b> — a category's department and the default branch are rows — so they cannot run
/// during option binding. They live in <see cref="ValidateAgainstDatabaseAsync"/>, which the
/// <c>DatabaseInitializer</c> calls <b>after</b> migrations and seeding, for the obvious reason that
/// the rows they check are created by seeding.
/// </para>
///
/// <para><b>Every message names the offending value</b>, because a startup failure that says only
/// "configuration is invalid" costs more time than no validation at all.</para>
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// <b>Checks 1 and 3</b> — the two that read rows.
    /// <list type="number">
    ///   <item>Every configured category maps to a department that <b>exists</b> (A-14).</item>
    ///   <item><c>Registration:DefaultBranchId</c> references a branch that <b>exists</b> (A-15).</item>
    /// </list>
    /// <para>
    /// Throws <see cref="OptionsValidationException"/> so a database-backed failure is
    /// indistinguishable, to the operator, from a binding-time one: same exception type, same shape
    /// of message, host stops either way.
    /// </para>
    /// </summary>
    public static async Task ValidateAgainstDatabaseAsync(
        IApplicationDbContext db,
        CategoryOptions categories,
        RegistrationOptions registration,
        CancellationToken ct)
    {
        var failures = new List<string>();

        // One round trip, not one per category: the whole point is a fast, clear startup, and a
        // per-row query would scale with the category list for no benefit.
        var departmentIds = await db.Departments.AsNoTracking().Select(d => d.Id).ToListAsync(ct);

        foreach (var category in categories.Items)
        {
            if (!departmentIds.Contains(category.DepartmentId))
            {
                // The category code is in the message because that is what the operator has to go
                // and fix — A-14: "an unmapped category is a configuration error and fails at
                // startup validation".
                failures.Add(
                    $"{CategoryOptions.SectionName}: category '{category.Code}' maps to departmentId " +
                    $"'{category.DepartmentId}', which is not an existing department. Every category " +
                    "must map to a department that exists (A-14).");
            }
        }

        if (!await db.Branches.AsNoTracking().AnyAsync(b => b.Id == registration.DefaultBranchId, ct))
        {
            failures.Add(
                $"{RegistrationOptions.SectionName}:DefaultBranchId '{registration.DefaultBranchId}' is not " +
                "an existing branch. Self-registering customers are assigned this branch (A-15).");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(
                nameof(ConfigurationValidator), typeof(CategoryOptions), failures);
        }
    }
}

/// <summary>
/// Structural checks on the category list. The <b>referential</b> check — that the department
/// exists — is <see cref="ConfigurationValidator.ValidateAgainstDatabaseAsync"/>, because it needs
/// rows.
/// </summary>
public sealed class CategoryOptionsValidator : IValidateOptions<CategoryOptions>
{
    public ValidateOptionsResult Validate(string? name, CategoryOptions options)
    {
        var failures = new List<string>();

        if (options.Items.Count == 0)
        {
            failures.Add($"{CategoryOptions.SectionName}:Items must list at least one category (A-6).");
        }

        foreach (var category in options.Items)
        {
            if (string.IsNullOrWhiteSpace(category.Code))
            {
                failures.Add($"{CategoryOptions.SectionName}: a category has no Code.");
            }

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                failures.Add($"{CategoryOptions.SectionName}: category '{category.Code}' has no Name.");
            }

            // A-14 requires a mapping for every category. An empty GUID is the shape a missing
            // mapping takes after binding, so it is caught here rather than reaching the database
            // check as a confusing "not an existing department".
            if (category.DepartmentId == Guid.Empty)
            {
                failures.Add(
                    $"{CategoryOptions.SectionName}: category '{category.Code}' has no DepartmentId. " +
                    "Every category must map to a department (A-14).");
            }
        }

        // Codes are the identifiers clients send, so two categories sharing one would make the
        // routing map ambiguous — which department wins would be an accident of ordering.
        var duplicates = options.Items
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            failures.Add($"{CategoryOptions.SectionName}: category code '{duplicate}' is configured more than once.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// <b>Check 4</b> — the configured priority list equals the approved levels, <b>in order</b>.
/// <para>
/// A-6 fixes four levels. Configuration exists so the values have one home, <b>not</b> so they can
/// be changed: a fifth level, a renamed one or a reordered list would silently break escalation
/// (which raises priority "one level", Story 06) and the SLA target lookup.
/// </para>
/// </summary>
public sealed class PriorityOptionsValidator : IValidateOptions<PriorityOptions>
{
    public ValidateOptionsResult Validate(string? name, PriorityOptions options)
    {
        if (options.Levels.SequenceEqual(PriorityOptions.ApprovedLevels, StringComparer.Ordinal))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{PriorityOptions.SectionName}:Levels must be exactly " +
            $"[{string.Join(", ", PriorityOptions.ApprovedLevels)}], in that order (A-6). " +
            $"Configured: [{string.Join(", ", options.Levels)}]. " +
            "Configuration may not add, rename or reorder a priority level.");
    }
}

/// <summary>
/// <b>Check 2</b> — every priority has an SLA target, and both hour values are greater than zero
/// (A-3).
/// <para>
/// A missing target is not a default-to-something situation: a ticket at that priority would have
/// no due date at all, and Story 09's breach sweep would never fire for it.
/// </para>
/// </summary>
public sealed class SlaTargetOptionsValidator : IValidateOptions<SlaTargetOptions>
{
    public ValidateOptionsResult Validate(string? name, SlaTargetOptions options)
    {
        var failures = new List<string>();

        foreach (var level in PriorityOptions.ApprovedLevels)
        {
            var matches = options.Items
                .Where(t => string.Equals(t.Priority, level, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                failures.Add(
                    $"{SlaTargetOptions.SectionName}: priority '{level}' has no SLA target. " +
                    "Every priority must have a first-response and a resolution target (A-3).");
                continue;
            }

            if (matches.Count > 1)
            {
                failures.Add($"{SlaTargetOptions.SectionName}: priority '{level}' has more than one SLA target.");
            }

            foreach (var target in matches)
            {
                if (target.FirstResponseHours <= 0)
                {
                    failures.Add(
                        $"{SlaTargetOptions.SectionName}: priority '{level}' has FirstResponseHours " +
                        $"{target.FirstResponseHours}; it must be greater than zero (A-3).");
                }

                if (target.ResolutionHours <= 0)
                {
                    failures.Add(
                        $"{SlaTargetOptions.SectionName}: priority '{level}' has ResolutionHours " +
                        $"{target.ResolutionHours}; it must be greater than zero (A-3).");
                }
            }
        }

        // A target for something that is not a priority is a typo the operator wants to hear about,
        // not a harmless extra row.
        foreach (var target in options.Items)
        {
            if (!PriorityOptions.ApprovedLevels.Contains(target.Priority, StringComparer.Ordinal))
            {
                failures.Add(
                    $"{SlaTargetOptions.SectionName}: '{target.Priority}' is not a priority level. " +
                    $"Expected one of [{string.Join(", ", PriorityOptions.ApprovedLevels)}] (A-6).");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// <b>Check 5</b> — <c>Min &lt; Max</c>, and <b>nothing else</b>.
/// <para>
/// <b>This asserts nothing about which values are correct: OQ-1 is open.</b> A 1–5 scale, a 1–10
/// scale and a 0–1 binary scale all pass, and they are meant to — docs/architecture.md §6.3 says the
/// boundary values are deliberately not decided, and inventing them here would bury a product
/// decision in a validator. The check exists only so a scale that cannot render — an inverted or
/// empty range — fails at startup rather than in Story 13's control.
/// </para>
/// </summary>
public sealed class FeedbackOptionsValidator : IValidateOptions<FeedbackOptions>
{
    public ValidateOptionsResult Validate(string? name, FeedbackOptions options)
    {
        if (options.Min < options.Max)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{FeedbackOptions.SectionName}: Min ({options.Min}) must be less than Max ({options.Max}). " +
            "This is a structural check only — which values are correct is OQ-1, and it is open.");
    }
}

/// <summary>
/// <b>Check 6</b> — the attachment cap is a positive number of bytes (T2-A) — and, added by
/// Story 04, that the storage root is set.
/// <para>
/// The root is <b>not</b> probed for existence or writability here.
/// <c>LocalDiskAttachmentStorage</c> creates it on first use, so a missing directory is not a
/// failure; a missing <em>setting</em> is, because the alternative is an upload handler writing to
/// some path nobody chose.
/// </para>
/// </summary>
public sealed class AttachmentOptionsValidator : IValidateOptions<AttachmentOptions>
{
    public ValidateOptionsResult Validate(string? name, AttachmentOptions options)
    {
        var failures = new List<string>();

        if (options.MaxSizeBytes <= 0)
        {
            failures.Add(
                $"{AttachmentOptions.SectionName}:MaxSizeBytes must be greater than zero; " +
                $"configured {options.MaxSizeBytes}.");
        }

        if (string.IsNullOrWhiteSpace(options.StorageRoot))
        {
            failures.Add(
                $"{AttachmentOptions.SectionName}:StorageRoot must be set — the directory attachment " +
                "bytes are written under (T2-A, local disk by design).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// Structural checks on the quick-reply library (T1-C). The list may legitimately be <b>empty</b> —
/// no approved document requires a canned reply to exist — so emptiness is not a failure; a
/// malformed entry is.
/// </summary>
public sealed class QuickReplyOptionsValidator : IValidateOptions<QuickReplyOptions>
{
    public ValidateOptionsResult Validate(string? name, QuickReplyOptions options)
    {
        var failures = new List<string>();

        foreach (var reply in options.Items)
        {
            if (string.IsNullOrWhiteSpace(reply.Id))
            {
                failures.Add($"{QuickReplyOptions.SectionName}: a quick reply has no Id.");
            }

            if (string.IsNullOrWhiteSpace(reply.Title))
            {
                failures.Add($"{QuickReplyOptions.SectionName}: quick reply '{reply.Id}' has no Title.");
            }

            if (string.IsNullOrWhiteSpace(reply.Body))
            {
                failures.Add($"{QuickReplyOptions.SectionName}: quick reply '{reply.Id}' has no Body.");
            }
        }

        var duplicates = options.Items
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            failures.Add($"{QuickReplyOptions.SectionName}: quick reply id '{duplicate}' is configured more than once.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// <c>SupportCrm:Ai</c> — Story 10, docs/architecture.md §5.1, §6.3.
///
/// <para>
/// <b>The default configuration is always valid</b>, because the default is the fake: with nothing
/// configured the application starts and every AI capability answers offline (A-7, product-scope §10
/// item 5). Validation only has something to say once a real provider is deliberately selected.
/// </para>
///
/// <para>
/// <b>A selected provider with no credential fails at startup, with a sentence.</b> The alternative is
/// a confusing <c>401</c> reaching the first agent who presses <em>Summarize</em> — a misconfiguration
/// should be an operator's problem at boot, not a user's problem at random.
/// </para>
/// </summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ = name;

        if (options.Provider != AiProviderKind.Provider)
        {
            // The fake needs no endpoint, no key and no network. Nothing to check.
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            failures.Add(
                $"{AiOptions.SectionName}: Endpoint is required when Provider is 'Provider'. "
                + "Set it, or leave Provider unset to use the offline fake.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add(
                $"{AiOptions.SectionName}: ApiKey is required when Provider is 'Provider'. "
                + "Supply it through the environment (SupportCrm__Ai__ApiKey); it is never committed.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
