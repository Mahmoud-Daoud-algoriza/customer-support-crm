using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// <b>Who is notified when a ticket is escalated — the one implementation of A-21.</b>
///
/// <para>
/// T2-D words the escalation rule as <em>"flag the ticket breached, raise priority one level,
/// notify the department manager"</em>, but <c>Department.ManagerUserId</c> is optional
/// (docs/data-model.md §2.2), so the nominal recipient may not exist. That gap was
/// <b>OQ-3</b>, open from 2026-08-24 and deliberately left uninvented by the data model. It was
/// closed on <b>2026-08-31</b> as <b>A-21</b> (docs/product-scope.md §7): the notification
/// escalates to the next authority level rather than being dropped.
/// </para>
///
/// <para>
/// <b>This interface exists so the rule has exactly one home.</b> Two call sites resolve
/// recipients — Story 06's <b>manual</b> <c>POST /tickets/{id}/escalate</c> and Story 09's
/// <b>automatic</b> breach sweep — and the intake for Story 09 requires the automatic trigger to
/// reuse the manual escalation path rather than duplicate it. A cascade re-expressed at two call
/// sites is a cascade that will drift, which is the same reasoning that put A-20 behind the single
/// named <c>SlaClock.OnPriorityChanged</c> rather than inlining it.
/// </para>
///
/// <para>
/// <b>Escalation is never blocked by a missing recipient.</b> A-21 and docs/data-model.md §2.2 both
/// state that the breach flag and the priority raise are unaffected by the absence of a manager.
/// This policy therefore resolves recipients and nothing else: it throws no exception for an empty
/// result, and a caller must treat <see cref="EscalationRecipientTier.None"/> as "notify nobody",
/// never as "do not escalate".
/// </para>
///
/// <para>
/// <b>No contract surface changes because of this rule.</b> <c>Notification</c> is recipient-scoped
/// (docs/data-model.md §2.12) and no response field names a recipient, so which tier fired is
/// observable only in whose notification list gains a row.
/// </para>
/// </summary>
public interface IEscalationRecipientPolicy
{
    /// <summary>
    /// Resolves the recipients for an escalation on a ticket in <paramref name="departmentId"/>,
    /// applying the A-21 cascade. Never throws for an absent manager.
    /// </summary>
    Task<EscalationRecipients> ResolveAsync(Guid departmentId, CancellationToken ct);
}

/// <summary>
/// Which rung of the A-21 cascade produced the recipients. Returned rather than inferred so a
/// caller — and a test — can tell "the department manager was notified" from "the department has no
/// manager, so every Manager was", without re-deriving the rule it just asked this policy to apply.
/// </summary>
public enum EscalationRecipientTier
{
    /// <summary>A-21 rung 1 — the department's own manager, who is set, active and still eligible.</summary>
    DepartmentManager = 0,

    /// <summary>A-21 rung 2 — no usable department manager, so every active <see cref="UserRole.Manager"/>.</summary>
    AllManagers = 1,

    /// <summary>A-21 rung 3 — no active Manager either, so every active <see cref="UserRole.Administrator"/>.</summary>
    AllAdministrators = 2,

    /// <summary>
    /// A-21's terminal case — nobody eligible exists. <b>The escalation still happens</b>; only the
    /// notification has no recipient.
    /// </summary>
    None = 3,
}

/// <summary>
/// The resolved recipients, and the rung that produced them.
/// <para>
/// <b><see cref="UserIds"/> is ordered and therefore deterministic.</b> A set that varied between
/// calls would make the notification rows a story could assert about unstable, so the
/// implementation orders it rather than returning whatever the provider happened to yield.
/// </para>
/// </summary>
public sealed record EscalationRecipients(EscalationRecipientTier Tier, IReadOnlyList<Guid> UserIds)
{
    /// <summary>The terminal case of A-21 — resolved nobody, which is not an error.</summary>
    public static EscalationRecipients None { get; } =
        new(EscalationRecipientTier.None, Array.Empty<Guid>());

    /// <summary><see langword="true"/> when rung 2 or 3 fired — i.e. the department had no usable manager.</summary>
    public bool IsFallback => Tier != EscalationRecipientTier.DepartmentManager;
}
