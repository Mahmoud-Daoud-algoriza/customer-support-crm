using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Application.Modules.Organization;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Infrastructure.Notifications;
using SupportCrm.Infrastructure.Persistence.Seeders;
using SupportCrm.Infrastructure.Security;
using SupportCrm.Infrastructure.Storage;

namespace SupportCrm.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The connection string comes from configuration only — never from a committed file
    /// (docs/architecture.md §6.3). Compose supplies it as
    /// <c>ConnectionStrings__SupportCrm</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SupportCrm")
            ?? throw new InvalidOperationException(
                "Connection string 'SupportCrm' is not configured. Set ConnectionStrings__SupportCrm " +
                "in the environment (see .env.example).");

        services.AddDbContext<SupportCrmDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Application orchestrates persistence through this abstraction and never names the
        // concrete context (docs/architecture.md §2.1, AD-2).
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<SupportCrmDbContext>());

        services.AddHostedService<DatabaseInitializer>();

        // Seeders run in ascending Order after migrations (AD-8).
        services.AddScoped<IDataSeeder, OrganizationSeeder>();
        services.AddScoped<IDataSeeder, IdentitySeeder>();
        services.AddScoped<IDataSeeder, CustomerSeeder>();
        services.AddScoped<IDataSeeder, TicketSeeder>();

        // ASP.NET Core's standard password hashing (docs/architecture.md §4.1). No password policy
        // engine, no account recovery — both out of scope.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // The token seam: declared by Application, implemented here (AD-11).
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Story 04's storage seam, the same shape: Application declares IAttachmentStorage,
        // Infrastructure implements it on local disk (T2-A, docs/architecture.md §4.4, §5).
        // Singleton — it holds only the resolved root and writes a fresh file per call.
        services.AddSingleton<IAttachmentStorage, LocalDiskAttachmentStorage>();

        services.AddSingleton(TimeProvider.System);

        // Story 02 Application services.
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();

        // Story 03 Application services. DepartmentValidator is registered even though no endpoint
        // reaches it: IdentitySeeder's manager second pass is its caller, and there is no write
        // endpoint for a department by design (T2-I).
        services.AddScoped<OrganizationQueryService>();
        services.AddScoped<DepartmentValidator>();

        // Story 04 Application services. They are registered even though no endpoint reaches them
        // yet: the controllers are task 8, and registering them here is what proves each service
        // resolves from the real composition root with its real dependencies.
        services.AddScoped<CustomerService>();
        services.AddScoped<CustomerNoteService>();
        services.AddScoped<CustomerTimelineService>();
        services.AddScoped<AttachmentService>();

        // Story 05 Application services.
        services.AddScoped<TicketService>();
        services.AddScoped<TicketActivityRecorder>();

        // The automatic-assignment seam. Story 05 delivers MANUAL assignment only (the ticket-core
        // intake), so the registered policy assigns nobody; Story 09 replaces it with round-robin
        // across active agents in the ticket's department (T2-D). Registering the no-op now is what
        // lets TicketService.CreateAsync depend on the seam rather than on its absence.
        services.AddScoped<IAutoAssignmentPolicy, NoAutoAssignmentPolicy>();

        // Story 06 Application services — the lifecycle half of the ticket module.
        services.AddScoped<TicketLifecycleService>();
        services.AddScoped<TicketActivityQueryService>();

        // The notification seam (A-13). Story 06 registers the LOGGING implementation because the
        // Notification entity is Story 09's; Story 09 replaces this ONE line with the persistent
        // publisher that writes rows. The interface, the call sites and the type set do not change.
        services.AddScoped<INotificationPublisher, LoggingNotificationPublisher>();

        // The escalation-recipient seam — A-21, closing OQ-3 (docs/product-scope.md §7).
        // Story 06's manual escalate and Story 09's automatic breach sweep resolve recipients
        // through ONE policy, which is what makes the cascade shared rather than a rule each story
        // re-expresses. Story 06's TicketLifecycleService.EscalateAsync is now its first caller.
        services.AddScoped<IEscalationRecipientPolicy, EscalationRecipientPolicy>();

        return services;
    }
}
