using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Wolverine;

using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.Endpoints;

/// <summary>Request body for POST /proxyhosts.</summary>
public sealed record CreateProxyHostRequest(
    IEnumerable<string>? DomainNames,
    string? DestinationUri,
    string? CertificatePath,
    string? CertificateKeyPath);

/// <summary>Request body for PUT /proxyhosts/{id}.</summary>
public sealed record UpdateProxyHostRequest(
    IEnumerable<string>? DomainNames,
    string? DestinationUri,
    bool? IsEnabled,
    string? CertificatePath,
    string? CertificateKeyPath);

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

        group.MapPost("/", async Task<Results<Created<ProxyHostDto>, ProblemHttpResult>> (
            [FromBody] CreateProxyHostRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.DestinationUri))
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: "'destinationUri' must be a valid absolute http or https URI.");

            var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? "unknown";

            var command = new CreateProxyHostCommand(
                request.DomainNames ?? [],
                request.DestinationUri,
                request.CertificatePath,
                request.CertificateKeyPath,
                actorId);

            try
            {
                var dto = await bus.InvokeAsync<ProxyHostDto>(command, ct);
                return TypedResults.Created($"/proxyhosts/{dto.Id}", dto);
            }
            catch (ProxyHostValidationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: ex.Message);
            }
            catch (ProxyHostConflictException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: ex.Message);
            }
        });

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
            Guid id,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? "unknown";

            try
            {
                await bus.InvokeAsync(new DeleteProxyHostCommand(id, actorId), ct);
                return TypedResults.NoContent();
            }
            catch (ProxyHostNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Proxy host not found",
                    detail: ex.Message);
            }
        });

        group.MapPut("/{id:guid}", async Task<Results<Ok<ProxyHostDto>, ProblemHttpResult>> (
            Guid id,
            [FromBody] UpdateProxyHostRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? "unknown";

            var command = new UpdateProxyHostCommand(
                id,
                request.DomainNames,
                request.DestinationUri,
                request.IsEnabled,
                request.CertificatePath,
                request.CertificateKeyPath,
                actorId);

            try
            {
                var dto = await bus.InvokeAsync<ProxyHostDto>(command, ct);
                return TypedResults.Ok(dto);
            }
            catch (ProxyHostNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Proxy host not found",
                    detail: ex.Message);
            }
            catch (ProxyHostValidationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: ex.Message);
            }
        });

        return app;
    }
}
