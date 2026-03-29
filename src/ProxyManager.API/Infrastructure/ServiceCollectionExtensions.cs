using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Infrastructure.Repositories;

namespace West94.ProxyManager.API.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers in-memory repository implementations for proxy hosts and audit log.</summary>
    public static IServiceCollection AddProxyManagerServices(this IServiceCollection services)
    {
        services.AddSingleton<IProxyHostRepository, InMemoryProxyHostRepository>();
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
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
