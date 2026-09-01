using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Infrastructure.Seams.Ai;
using SupportCrm.Application.Modules.Ai;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Application.Modules.Knowledge;
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
        services.AddScoped<IDataSeeder, KnowledgeSeeder>();

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
        // Story 09 — round-robin across active agents in the ticket's department (T2-D). It replaces
        // Story 05's no-op at the seam CreateAsync already calls; the creation path needed no edit.
        services.AddScoped<IAutoAssignmentPolicy, RoundRobinAssignmentPolicy>();

        // Story 06 Application services — the lifecycle half of the ticket module.
        services.AddScoped<TicketLifecycleService>();
        services.AddScoped<TicketActivityQueryService>();

        // The notification seam (A-13). Story 06 registers the LOGGING implementation because the
        // Notification entity is Story 09's; Story 09 replaces this ONE line with the persistent
        // publisher that writes rows. The interface, the call sites and the type set do not change.
        // Story 09 — the swap Story 06 promised: same interface, same call sites, rows instead of a log
        // line. LoggingNotificationPublisher was deleted, so there is one implementation.
        services.AddScoped<INotificationPublisher, PersistentNotificationPublisher>();

        // Story 07 Application services — the channel seam's one ingestion service and the
        // portal's own submission path. TicketMessageService is what Story 18's log adapter calls
        // IN-PROCESS; it gets no HTTP route of its own (AP-11), which is how PF-2 stays untouched.
        services.AddScoped<TicketMessageService>();
        services.AddScoped<PortalTicketService>();

        // Story 13 — the sole CSAT input (requirements §8.5, T2-F). It is registered beside the
        // ticket services because CustomerFeedback belongs to the Tickets module (DM-7); there is no
        // Portal backend module to register it in, and no eleventh module was added.
        services.AddScoped<CustomerFeedbackService>();

        // The escalation-recipient seam — A-21, closing OQ-3 (docs/product-scope.md §7).
        // Story 06's manual escalate and Story 09's automatic breach sweep resolve recipients
        // through ONE policy, which is what makes the cascade shared rather than a rule each story
        // re-expresses. Story 06's TicketLifecycleService.EscalateAsync is now its first caller.
        services.AddScoped<IEscalationRecipientPolicy, EscalationRecipientPolicy>();

        // Story 09 Application services — the SLA sweep and the notification read side. The sweep is
        // scoped, not singleton: the hosted service creates a scope per tick (AD-6).
        services.AddScoped<SlaEvaluationService>();
        services.AddScoped<NotificationService>();

        // ------------------------------------------------------------------ Story 10: the AI seam
        // **One interface, two implementations, chosen by configuration** (architecture §5.1, AD-11).
        //
        // **The fake is the default**, so the application starts and every AI capability answers with
        // no configuration, no credential and no network at all — A-7 and product-scope §10 item 5
        // require exactly that, and the intake is blunt about why: if the provider is unavailable on
        // demo day, the fake IS the demo.
        //
        // The provider adapter is registered through `AddHttpClient` so it gets a pooled handler; the
        // fake takes no HttpClient and a test asserts it never will.
        services.AddHttpClient<ProviderAiService>();

        // Story 11 — the three agent-facing assists that consume the seam.
        services.AddScoped<TicketAiAssistService>();

        services.AddScoped<IAiAssistService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;

            return options.Provider == AiProviderKind.Provider
                ? sp.GetRequiredService<ProviderAiService>()
                : ActivatorUtilities.CreateInstance<DeterministicFakeAiService>(sp);
        });

        // Story 12 Application services — the knowledge base. THREE services, one per audience:
        // staff/admin CRUD and search, the customer read, and §7.4's retrieval.
        //
        // **SuggestedArticleService is registered here, next to the other Knowledge services, and
        // NOT next to the AI seam above** (AP-14, architecture §5.1). It resolves no
        // IAiAssistService and must never be moved under /ai: it retrieves existing articles by
        // keyword (AD-13) rather than generating anything.
        services.AddScoped<KnowledgeArticleService>();
        services.AddScoped<PortalArticleService>();
        services.AddScoped<SuggestedArticleService>();

        // Story 16 Part B — the audit read surface. AuditRecorder (above) stays the only writer;
        // this is the one read method GET /audit exposes (T2-H).
        services.AddScoped<AuditQueryService>();

        return services;
    }
}
