using System.Security.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;

namespace West94.ProxyManager.Endpoints;

public sealed record LoginRequest(string Username, string Password, bool RememberMe, string? ReturnUrl);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/signin-oidc", async Task<Results<RedirectHttpResult, ProblemHttpResult>> (string? returnUrl) =>
        {
            if (!string.IsNullOrEmpty(returnUrl) && !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
            {       
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid return URL",
                    detail: "The provided return URL is not valid or is not a local URL."
                );
            }

            var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
            return TypedResults.Redirect(redirectUrl);
        })
        .ExcludeFromDescription();

        app.MapGet("/login", async Task<Results<ChallengeHttpResult, ProblemHttpResult>> (string? returnUrl) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(returnUrl) && !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
                {       
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid return URL",
                        detail: "The provided return URL is not valid or is not a local URL."
                    );
                }


                return TypedResults.Challenge(
                    authenticationSchemes: [OpenIdConnectDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = returnUrl ?? "/"
                    });
            }
            catch (AuthenticationException ex)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication failed",
                    detail: ex.Message);
            }
        })
        .ExcludeFromDescription();

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.Redirect("/");
        }).ExcludeFromDescription();

        return app;
    }
}