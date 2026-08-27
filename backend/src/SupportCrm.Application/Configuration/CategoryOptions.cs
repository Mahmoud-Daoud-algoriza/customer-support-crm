using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The ticket category list <b>and</b> the category → department map, in one place.
/// <para>
/// They are one option type because <b>A-14 makes them inseparable</b>: a customer chooses a
/// category and never a department, and the ticket takes its <c>departmentId</c> from this mapping
/// at creation, before assignment. Splitting them into two sections would allow a category to exist
/// with no mapping, which is exactly the configuration error startup validation refuses
/// (docs/architecture.md §6.3, ConfigurationValidator check 1).
/// </para>
/// <para>
/// <b>Categories are configuration, not a table</b> (docs/data-model.md §2.16, A-6). No
/// <c>Category</c> entity exists and none may be added — a test asserts it.
/// </para>
/// </summary>
public sealed class CategoryOptions
{
    public const string SectionName = "SupportCrm:Categories";

    /// <summary>
    /// A <b>flat</b> list — a fixed enumeration, never a user-managed taxonomy, and never a
    /// hierarchy (A-6). There is no parent, no ordering key and no enabled flag, because no
    /// approved document asks for one.
    /// </summary>
    /// <remarks>
    /// The wrapper property exists for a binding reason only: <c>AddOptions&lt;T&gt;().Bind()</c>
    /// binds a section to an object, and a bare JSON array is not one. No approved document fixes
    /// the physical shape of the section, so <c>Items</c> is an implementation choice, applied the
    /// same way by every list-bearing options type here.
    /// </remarks>
    [Required, MinLength(1)]
    public List<CategoryOption> Items { get; init; } = [];
}

/// <summary>One configured category. The <c>code</c> is the stable identifier clients send.</summary>
public sealed class CategoryOption
{
    /// <summary>
    /// The stable code — what <c>POST /portal/tickets</c> and <c>POST /tickets</c> accept, and what
    /// <c>GET /config</c> publishes (docs/api-design.md §6.9). Never a display string.
    /// </summary>
    [Required] public string Code { get; init; } = default!;

    /// <summary>The display name. Published to every authenticated caller.</summary>
    [Required] public string Name { get; init; } = default!;

    /// <summary>
    /// The department every ticket in this category is routed to (A-14).
    /// <para>
    /// <b>Never published to a Customer.</b> <c>GET /config</c> carries <c>code</c> and <c>name</c>
    /// only; the routing map is staff-only, on <c>GET /config/staff</c> (AP-17, B-2). A customer
    /// chooses a category and does not learn where it goes.
    /// </para>
    /// It must reference a department that exists — checked at startup against the database, because
    /// options binding cannot see rows (ConfigurationValidator check 1).
    /// </summary>
    [Required] public Guid DepartmentId { get; init; }
}
