using Microsoft.AspNetCore.Authentication.Cookies;
using West94.ProxyManager.Yarp;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Scalar.AspNetCore;

using Serilog;
using Serilog.Events;
using West94.ProxyManager.Endpoints;
using Microsoft.AspNetCore.Authentication;
using West94.AspNetCore.Authentication;
using System.Security.Claims;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try 
{
    var builder = WebApplication.CreateBuilder(args);
    var services = builder.Services;
    var configuration = builder.Configuration;

    var proxySettingsFile = $"proxysettings.{builder.Environment.EnvironmentName}.json";
    configuration.AddJsonFile(proxySettingsFile, optional: true, reloadOnChange: true);

    services.AddReverseProxy()
        .LoadFromConfig(configuration.GetSection("ReverseProxy"))
        .AddTransformFactory<BearerTokenTransformFactory>()
        .AddTransformFactory<ClaimHeaderTransformFactory>();

    services.AddHttpContextAccessor();
    services.AddHttpClient();

    services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Check if access token is present in the authentication properties
                var accessToken = context.Properties.GetTokenValue("access_token");
                if (string.IsNullOrEmpty(accessToken)) {
                    // No access token, reject the principal
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);  
                    return;
                }

                // if the access token has expired and a refresh token is available, attempt to refresh the tokens
                var refreshToken = context.Properties.GetTokenValue("refresh_token");
                var now = DateTimeOffset.UtcNow;
                var expiresAt = DateTimeOffset.Parse(context.Properties.Items[".Token.expires_at"]!);
                var leeway = 60;
                var difference = DateTimeOffset.Compare(expiresAt, now.AddSeconds(leeway));
                var isExpired = difference <= 0;
                
                if (isExpired && !string.IsNullOrEmpty(refreshToken))
                {
                    var httpClient = context.HttpContext.RequestServices.GetRequiredService<HttpClient>();
                    var tokenClient = new TokenClient(httpClient);

                    // Attempt to refresh the tokens using the refresh token
                    var result = await tokenClient.RefreshToken(refreshToken, configuration["Authentication:Authority"]);

                    if (result != null)
                    {
                        // Update the authentication properties with the new tokens and expiration time
                        context.Properties.UpdateTokenValue("access_token", result.AccessToken);

                        if (!string.IsNullOrEmpty(result.RefreshToken)){
                            context.Properties.UpdateTokenValue("refresh_token", result.RefreshToken);
                        }

                        context.Properties.UpdateTokenValue("id_token", result.IdToken);
                        context.Properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn).ToString("o"));
                    }
                    else
                    {
                        // Token refresh failed, reject the principal
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);  
                    }

                    context.ShouldRenew = true;
                }
                    
            }
        };
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = configuration["Authentication:Authority"];
        options.ClientId = configuration["Authentication:ClientId"];
        options.ClientSecret = configuration["Authentication:ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = configuration["Authentication:CallbackPath"] ?? "/signin-oidc";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.ClaimActions.MapJsonKey("sub", "sub");
        options.ClaimActions.MapJsonKey("groups", "groups", ClaimValueTypes.String);
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
    });

    services.AddAuthorization();

    services.AddAuthorizationBuilder()
        .AddPolicy("AuthenticatedUsersOnly", policy => policy.RequireAuthenticatedUser());

    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    services.AddOpenApi();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference((options) =>
        {
                options
                    .SortOperationsByMethod()
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithTitle("Proxy Manager API")
                    .WithTheme(ScalarTheme.Mars);
        });
    }

    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapReverseProxy();

    app.MapAccountEndpoints();
    app.MapFallbackToFile("404.html");

    Log.Information("Starting Proxy Manager host...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}