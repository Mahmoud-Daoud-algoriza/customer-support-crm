using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Infrastructure.Persistence;

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

        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
