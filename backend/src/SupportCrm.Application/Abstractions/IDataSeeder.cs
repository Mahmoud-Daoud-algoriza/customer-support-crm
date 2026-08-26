namespace SupportCrm.Application.Abstractions;

/// <summary>
/// A unit of demo data. Implementations arrive with the story that owns each concept
/// (docs/architecture.md AD-8); Story 01 delivers the mechanism only.
/// Seeders run in ascending <see cref="Order"/> after migrations, at API startup.
/// </summary>
public interface IDataSeeder
{
    int Order { get; }

    Task SeedAsync(CancellationToken ct);
}
