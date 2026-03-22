using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.Endpoints;

public static class ProxyHostEndpoints
{
    public static IEndpointRouteBuilder MapProxyHostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/proxyhosts")
            .WithTags("ProxyHosts")
            .RequireAuthorization();

        group.MapGet("/", async (IMessageBus bus, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var result = await bus.InvokeAsync<PagedResult<ProxyHostDto>>(new GetProxyHostsQuery(page, pageSize), ct);
            return TypedResults.Ok(result);
        });

        group.MapGet("/{id:guid}", async Task<Results<Ok<ProxyHostDto>, ProblemHttpResult>> (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var dto = await bus.InvokeAsync<ProxyHostDto?>(new GetProxyHostByIdQuery(id), ct);
            if (dto is not null)
                return TypedResults.Ok(dto);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Proxy host not found",
                detail: $"No proxy host with id '{id}' was found.");
        });

        return app;
    }
}
