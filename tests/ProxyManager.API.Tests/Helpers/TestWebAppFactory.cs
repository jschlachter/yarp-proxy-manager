using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using West94.ProxyManager.API.Infrastructure.Files;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Infrastructure.Data;

namespace West94.ProxyManager.API.Tests.Helpers;

/// <summary>
/// Integration test host that:
/// - Starts a disposable PostgreSQL TestContainer for each test class
/// - Runs EF Core migrations before tests
/// - Disables RabbitMQ transport
/// - Replaces Authentik JWT validation with a test signing key
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("proxymanager_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private bool _containerStarted;

    /// <summary>Shared fake substituted for the real Files service HTTP client — no live ProxyManager.Files needed in tests.</summary>
    public FakeFileAssetClient FilesClient { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (!_containerStarted)
        {
            _postgres.StartAsync().GetAwaiter().GetResult();
            _containerStarted = true;
        }

        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Enabled"] = "false",
                ["Authentication:Authority"] = TestJwtFactory.TestIssuer,
                ["Authentication:Audience"] = TestJwtFactory.TestAudience,
                ["Database:ConnectionString"] = _postgres.GetConnectionString()
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = null!;
                    options.MetadataAddress = null!;
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

            services.AddSingleton<IFileAssetClient>(FilesClient);
        });
    }
}
