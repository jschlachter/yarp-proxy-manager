using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Contracts;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Endpoints;

/// <summary>Request body for POST /files/{id}/commit.</summary>
public sealed record CommitFileAssetRequest(string? OwnerType, Guid? OwnerId);

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/files").WithTags("Files").RequireAuthorization();

        group.MapPost("/", UploadAsync);
        group.MapPost("/{id:guid}/commit", CommitAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/content", GetContentAsync);
        group.MapGet("/", ListAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<Results<Created<FileAssetDto>, ProblemHttpResult>> UploadAsync(
        HttpRequest request,
        [FromQuery] string? assetType,
        ClaimsPrincipal user,
        IFileAssetService assets,
        IOptions<UploadOptions> uploadOptions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assetType) || !AssetTypeAllowlist.IsAllowed(assetType))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: $"Asset type '{assetType}' is not recognized.");
        }

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
            string.IsNullOrEmpty(mediaType.Boundary.Value))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "Request must be multipart/form-data with a boundary.");
        }

        var maxBytes = uploadOptions.Value.MaxUploadBytes;
        var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = maxBytes + 65_536;
        }

        var reader = new MultipartReader(mediaType.Boundary.Value, request.Body);
        FileMultipartSection? fileSection = null;
        for (var section = await reader.ReadNextSectionAsync(ct); section is not null; section = await reader.ReadNextSectionAsync(ct))
        {
            fileSection = section.AsFileSection();
            if (fileSection is not null)
            {
                break;
            }
        }

        if (fileSection is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "No file part found in the request.");
        }

        using var buffer = new MemoryStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var readBuffer = new byte[81_920];
        long total = 0;
        int read;
        while ((read = await fileSection.FileStream!.ReadAsync(readBuffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Upload too large",
                    detail: $"Upload exceeds the {maxBytes}-byte limit.");
            }

            hash.AppendData(readBuffer, 0, read);
            await buffer.WriteAsync(readBuffer.AsMemory(0, read), ct);
        }

        buffer.Position = 0;
        var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());

        try
        {
            var asset = await assets.StageAsync(assetType, fileSection.FileName, buffer, total, sha256, user.GetActorId(), ct);
            return TypedResults.Created($"/files/{asset.Id}", asset.ToDto());
        }
        catch (UnsupportedAssetContentException ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported content",
                detail: ex.Message);
        }
        catch (FileAssetValidationException ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<FileAssetDto>, ProblemHttpResult>> CommitAsync(
        Guid id,
        [FromBody] CommitFileAssetRequest request,
        IFileAssetService assets,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerType) || request.OwnerId is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "'ownerType' and 'ownerId' are required.");
        }

        try
        {
            var asset = await assets.CommitAsync(id, request.OwnerType, request.OwnerId.Value, ct);
            return TypedResults.Ok(asset.ToDto());
        }
        catch (FileAssetNotFoundException ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "File asset not found",
                detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<FileAssetDto>, ProblemHttpResult>> GetAsync(
        Guid id, IFileAssetService assets, CancellationToken ct)
    {
        var asset = await assets.GetAsync(id, ct);
        return asset is not null
            ? TypedResults.Ok(asset.ToDto())
            : NotFound(id);
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetContentAsync(
        Guid id, IFileAssetService assets, CancellationToken ct)
    {
        var download = await assets.GetContentAsync(id, ct);
        return download is not null
            ? TypedResults.Stream(download.Content, download.Stat.ContentType)
            : NotFound(id);
    }

    private static async Task<Results<Ok<PagedResult<FileAssetDto>>, ProblemHttpResult>> ListAsync(
        [FromQuery] string? ownerType,
        [FromQuery] Guid? ownerId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IFileAssetService assets,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerType) || ownerId is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "'ownerType' and 'ownerId' are required.");
        }

        var result = await assets.ListAsync(ownerType, ownerId.Value, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id, IFileAssetService assets, CancellationToken ct)
    {
        var deleted = await assets.DeleteAsync(id, ct);
        return deleted ? TypedResults.NoContent() : NotFound(id);
    }

    private static ProblemHttpResult NotFound(Guid id) => TypedResults.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "File asset not found",
        detail: $"No file asset with id '{id}' was found.");
}
