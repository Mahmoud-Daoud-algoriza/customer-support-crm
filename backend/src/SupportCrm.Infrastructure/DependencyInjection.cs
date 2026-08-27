using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Application.Modules.Organization;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Infrastructure.Persistence.Seeders;
using SupportCrm.Infrastructure.Security;

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

        // ASP.NET Core's standard password hashing (docs/architecture.md §4.1). No password policy
        // engine, no account recovery — both out of scope.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // The token seam: declared by Application, implemented here (AD-11).
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

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

        return services;
    }
}
