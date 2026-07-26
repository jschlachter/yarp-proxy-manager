using System.Security.Claims;

namespace West94.ProxyManager.Files.Endpoints;

public static class ClaimsPrincipalExtensions
{
    public static string GetActorId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? "unknown";
}
