using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Wolverine;

using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.Endpoints;

/// <summary>Request body for POST /v1/users.</summary>
public sealed record CreateUserRequest(
    string? Sub,
    string? DisplayName,
    string? Nickname,
    string? Email,
    string? ProfileImageUrl,
    UserAccessLevel? AccessLevel);

/// <summary>Request body for PATCH /v1/users/{sub}.</summary>
public sealed record UpdateUserAccessLevelRequest(UserAccessLevel? NewAccessLevel);

/// <summary>Internal result from <c>CreateAuthorizedUserHandler</c> carrying the DTO and reactivation flag.</summary>
public sealed record CreateUserResult(AuthorizedUserDto Dto, bool Reactivated);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/users")
            .WithTags("Users")
            .RequireAuthorization();

        // GET /v1/users — list authorized users (paginated)
        group.MapGet("/", async (
            IMessageBus bus,
            bool includeDeactivated = false,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await bus.InvokeAsync<PagedResult<AuthorizedUserDto>>(
                new GetAuthorizedUsersQuery(includeDeactivated, page, pageSize), ct);
            return TypedResults.Ok(result);
        });

        // GET /v1/users/audit — audit log (literal segment declared BEFORE /{sub})
        group.MapGet("/audit", async (
            IMessageBus bus,
            string? sub = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await bus.InvokeAsync<PagedResult<UserAuditEntryDto>>(
                new GetUserAuditLogQuery(sub, from, to, page, pageSize), ct);
            return TypedResults.Ok(result);
        });

        // GET /v1/users/{sub} — retrieve single user
        group.MapGet("/{sub}", async Task<Results<Ok<AuthorizedUserDto>, ProblemHttpResult>> (
            string sub,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var dto = await bus.InvokeAsync<AuthorizedUserDto?>(new GetAuthorizedUserBySubQuery(sub), ct);
            if (dto is not null)
                return TypedResults.Ok(dto);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: $"No active user with sub '{sub}' was found.");
        });

        // POST /v1/users — create or reactivate user (Admin only)
        group.MapPost("/", async Task<Results<Created<AuthorizedUserDto>, Ok<AuthorizedUserDto>, ProblemHttpResult>> (
            [FromBody] CreateUserRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            HttpResponse response,
            CancellationToken ct) =>
        {
            var actorSub = user.FindFirstValue("sub") ?? "unknown";

            var command = new CreateAuthorizedUserCommand(
                request.Sub ?? string.Empty,
                request.DisplayName ?? string.Empty,
                request.Nickname ?? string.Empty,
                request.Email ?? string.Empty,
                request.ProfileImageUrl,
                request.AccessLevel ?? UserAccessLevel.ReadOnly,
                actorSub);

            try
            {
                var result = await bus.InvokeAsync<CreateUserResult>(command, ct);

                if (result.Reactivated)
                {
                    response.Headers["X-User-Reactivated"] = "true";
                    return TypedResults.Ok(result.Dto);
                }

                return TypedResults.Created($"/v1/users/{result.Dto.Sub}", result.Dto);
            }
            catch (UserConflictException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "User conflict",
                    detail: ex.Message);
            }
            catch (UserValidationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation error",
                    detail: ex.Message);
            }
        }).RequireAuthorization("UserAdmin");

        // PATCH /v1/users/{sub} — update access level (Admin only)
        group.MapPatch("/{sub}", async Task<Results<Ok<AuthorizedUserDto>, ProblemHttpResult>> (
            string sub,
            [FromBody] UpdateUserAccessLevelRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actorSub = user.FindFirstValue("sub") ?? "unknown";

            var command = new UpdateUserAccessLevelCommand(
                sub,
                request.NewAccessLevel ?? UserAccessLevel.ReadOnly,
                actorSub);

            try
            {
                var dto = await bus.InvokeAsync<AuthorizedUserDto>(command, ct);
                return TypedResults.Ok(dto);
            }
            catch (UserNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User not found",
                    detail: ex.Message);
            }
        }).RequireAuthorization("UserAdmin");

        // DELETE /v1/users/{sub} — deactivate user (Admin only)
        group.MapDelete("/{sub}", async Task<Results<NoContent, ProblemHttpResult>> (
            string sub,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actorSub = user.FindFirstValue("sub") ?? "unknown";

            try
            {
                await bus.InvokeAsync(new DeactivateUserCommand(sub, actorSub), ct);
                return TypedResults.NoContent();
            }
            catch (UserNotFoundException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User not found",
                    detail: ex.Message);
            }
        }).RequireAuthorization("UserAdmin");

        return app;
    }
}
