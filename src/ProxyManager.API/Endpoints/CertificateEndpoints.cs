using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Wolverine;

using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.Endpoints;

/// <summary>Request body for POST /certificates.</summary>
public sealed record CreateCertificateRequest(
    string? Name,
    string? Format,
    string? CertificatePath,
    string? KeyFilePath,
    string? PassPhrase);

/// <summary>Request body for PUT /certificates/{id}. Paths and format are immutable after creation.</summary>
public sealed record UpdateCertificateRequest(string? Name, string? PassPhrase);

public static class CertificateEndpoints
{
    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/certificates")
            .WithTags("Certificates")
            .RequireAuthorization();

        group.MapGet("/", async (IMessageBus bus, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var result = await bus.InvokeAsync<PagedResult<CertificateDto>>(new GetCertificatesQuery(page, pageSize), ct);
            return TypedResults.Ok(result);
        });

        group.MapGet("/{id:guid}", async Task<Results<Ok<CertificateDto>, ProblemHttpResult>> (
            Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var dto = await bus.InvokeAsync<CertificateDto?>(new GetCertificateByIdQuery(id), ct);
            if (dto is not null)
                return TypedResults.Ok(dto);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Certificate not found",
                detail: $"No certificate with id '{id}' was found.");
        });

        group.MapPost("/", async Task<Results<Created<CertificateDto>, ProblemHttpResult>> (
            [FromBody] CreateCertificateRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.CertificatePath))
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: "'certificatePath' is required.");

            var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? "unknown";

            var command = new CreateCertificateCommand(
                request.Name ?? string.Empty,
                request.Format ?? string.Empty,
                request.CertificatePath,
                request.KeyFilePath,
                request.PassPhrase,
                actorId);

            try
            {
                var dto = await bus.InvokeAsync<CertificateDto>(command, ct);
                return TypedResults.Created($"/certificates/{dto.Id}", dto);
            }
            catch (CertificateValidationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: ex.Message);
            }
        });

        group.MapPut("/{id:guid}", async Task<Results<Ok<CertificateDto>, ProblemHttpResult>> (
            Guid id,
            [FromBody] UpdateCertificateRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? "unknown";

            var command = new UpdateCertificateCommand(id, request.Name, request.PassPhrase, actorId);

            try
            {
                var dto = await bus.InvokeAsync<CertificateDto>(command, ct);
                return TypedResults.Ok(dto);
            }
            catch (CertificateNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Certificate not found",
                    detail: ex.Message);
            }
            catch (CertificateValidationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
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
                await bus.InvokeAsync(new DeleteCertificateCommand(id, actorId), ct);
                return TypedResults.NoContent();
            }
            catch (CertificateNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Certificate not found",
                    detail: ex.Message);
            }
        });

        return app;
    }
}
