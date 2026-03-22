using Wolverine;

namespace West94.ProxyManager.Endpoints;

public static class ProxyHostEndpoints
{
    public static IEndpointRouteBuilder MapProxyHostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/proxyhosts")
            .WithTags("ProxyHosts")
            .RequireAuthorization();

        return app;
    }
}
