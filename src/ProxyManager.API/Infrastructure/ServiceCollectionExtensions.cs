using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using West94.ProxyManager.Infrastructure.Extensions;
using West94.ProxyManager.Infrastructure.Options;

namespace West94.ProxyManager.API.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers PostgreSQL-backed repository implementations and the migration hosted service.</summary>
    public static IServiceCollection AddProxyManagerServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));
        services.AddProxyManagerInfrastructure();
        return services;
    }

    /// <summary>Registers OpenAPI with JWT Bearer security scheme and operation-level security requirements.</summary>
    public static IServiceCollection AddProxyManagerOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Enter your JWT Bearer token from Authentik."
                    }
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, ct) =>
            {
                var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Any();

                if (requiresAuth)
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("Bearer")] = []
                        }
                    ];
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
