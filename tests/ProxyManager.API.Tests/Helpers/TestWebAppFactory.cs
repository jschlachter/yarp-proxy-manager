using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace West94.ProxyManager.API.Tests.Helpers;

/// <summary>
/// Integration test host that:
/// - Disables RabbitMQ transport (uses Wolverine in-process only)
/// - Replaces Authentik JWT validation with a test signing key
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override configuration: disable RabbitMQ and point auth at test issuer/audience
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Enabled"] = "false",
                ["Authentication:Authority"] = TestJwtFactory.TestIssuer,
                ["Authentication:Audience"] = TestJwtFactory.TestAudience
            });
        });

        // Override JWT Bearer to validate test tokens with the known test signing key
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestJwtFactory.TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = TestJwtFactory.TestAudience,
                        ValidateLifetime = true,
                        IssuerSigningKey = TestJwtFactory.GetSigningKey(),
                        ValidateIssuerSigningKey = true
                    };
                });
        });
    }
}
