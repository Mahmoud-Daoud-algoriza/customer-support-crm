using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Infrastructure.Storage;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Shared plumbing for the Story 04 <b>slice 3</b> service tests (plan tasks 4, 5, 6 and 9).
///
/// <para>
/// None of those services has an endpoint yet — the controllers are task 8 — so each is exercised
/// directly against the <b>real</b> <see cref="SupportCrmDbContext"/> from the running composition
/// root. Only two things are substituted, and both for reasons that are about the missing HTTP
/// layer rather than about the behaviour under test:
/// </para>
/// <list type="bullet">
///   <item><see cref="ICurrentUser"/>, which <c>CurrentUserMiddleware</c> would have filled;</item>
///   <item><see cref="IAttachmentStorage"/>, rooted in a throwaway temp directory so a test run
///     never writes into the build output.</item>
/// </list>
/// </summary>
public sealed class CustomerModuleHarness : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), $"supportcrm-slice3-{Guid.NewGuid():N}");

    /// <summary>
    /// A local-disk store rooted in a temp directory. It is the <b>real</b>
    /// <see cref="LocalDiskAttachmentStorage"/>, not a fake — the round-trip assertions are about
    /// bytes actually reaching a disk and coming back.
    /// </summary>
    public LocalDiskAttachmentStorage Storage { get; }

    public CustomerModuleHarness(long maxSizeBytes = 1024 * 1024)
    {
        Directory.CreateDirectory(_storageRoot);

        Storage = new LocalDiskAttachmentStorage(
            Options.Create(new AttachmentOptions
            {
                MaxSizeBytes = maxSizeBytes,
                StorageRoot = "files",
            }),
            new HarnessHostEnvironment(_storageRoot),
            TimeProvider.System);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> against one scope of the real composition root, building the
    /// service under test with <see cref="ActivatorUtilities"/> so every dependency it does not
    /// override comes from the container exactly as it would in production.
    /// </summary>
    public static async Task<T> InScopeAsync<T>(
        SupportCrmApiFactory factory,
        Func<IServiceProvider, SupportCrmDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();

        return await work(
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>());
    }

    /// <summary>
    /// The caller a request would have carried. Every member is real — unlike the narrower stub the
    /// slice-2 tests use, because <c>AttachmentService</c> reads <see cref="DisplayName"/> for the
    /// <c>uploadedBy</c> summary and <see cref="IsInRoleAtLeast"/> for the AP-19 owner check.
    /// </summary>
    public sealed class Caller(Guid id, UserRole role, string displayName = "Test Caller") : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid Id { get; } = id;

        public UserRole Role { get; } = role;

        public Guid? DepartmentId => Role.IsStaff() ? Guid.Empty : null;

        public Guid? CustomerId { get; init; }

        public string DisplayName { get; } = displayName;

        public string Email => $"{Id:N}@test.local";

        /// <summary>The A-4 hierarchy check — Manager and Administrator satisfy <c>Agent</c>.</summary>
        public bool IsInRoleAtLeast(UserRole minimum) => Role.RankAtLeast(minimum);
    }

    /// <summary>
    /// A clock the test drives, so "newest first" is asserted against timestamps that are
    /// unambiguously ordered rather than against whatever resolution the machine happens to give.
    /// </summary>
    public sealed class StepClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            _now = _now.AddMinutes(1);

            return _now;
        }
    }

    /// <summary>Supplies only <see cref="IHostEnvironment.ContentRootPath"/>, which is all the storage reads.</summary>
    private sealed class HarnessHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(CustomerModuleHarness);

        public string ContentRootPath { get; set; } = contentRootPath;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
