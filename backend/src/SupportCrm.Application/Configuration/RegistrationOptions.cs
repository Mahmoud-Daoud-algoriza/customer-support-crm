using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// Settings for customer self-registration — <b>A-15</b>.
/// <para>
/// A-15: *"`Customer.branchId` is required (A-2). A self-registering customer is assigned the
/// <b>system default branch</b>, a configured value. They are not asked to choose one."* This is
/// that value's only home.
/// </para>
/// Consumed by Story 04's <c>POST /auth/register</c>.
/// </summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "SupportCrm:Registration";

    /// <summary>
    /// The branch assigned to every self-registering customer.
    /// <para>
    /// <b>The registration request cannot override it</b> — A-15 says the caller specifies no
    /// branch, no role and no customer id, and docs/api-design.md §7 lists
    /// <c>Customer.branchId</c> (registration) among the fields never accepted from a client.
    /// </para>
    /// It must reference a branch that exists — checked at startup against the database
    /// (ConfigurationValidator check 3).
    /// </summary>
    [Required] public Guid DefaultBranchId { get; init; }
}
