namespace SupportCrm.Domain.Modules.Administration;

/// <summary>
/// The two outcomes docs/data-model.md §2.14 names. Persisted as a stable string code
/// (docs/api-design.md §2).
/// </summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1,
}
