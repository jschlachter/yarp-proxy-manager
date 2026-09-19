extern alias ProxyManagerFilesApp;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using ProxyManagerFilesApp::West94.ProxyManager.Files.Options;
using ProxyManagerFilesApp::West94.ProxyManager.Files.Storage;
using West94.ProxyManager.API.Tests.Unit.Fakes;

namespace West94.ProxyManager.API.Tests.Helpers;

/// <summary>
/// Integration test host for ProxyManager.Files that swaps the real S3/RustFS-backed
/// <see cref="IObjectStore"/> for an in-memory fake, so tests exercise the real HTTP endpoints
/// (multipart upload parsing, staging, commit, content retrieval) without needing a live
/// RustFS/MinIO instance. Auth is satisfied via the service-token scheme, matching how
/// ProxyManager.API/ProxyManager talk to Files in production.
/// </summary>
public sealed class TestFilesAppFactory : WebApplicationFactory<ProxyManagerFilesApp::Program>
{
    public const string ServiceToken = "test-service-token";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("proxymanager_files_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private bool _containerStarted;

    public FakeFilesObjectStore ObjectStore { get; } = new();

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
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                ["ServiceToken:SharedSecret"] = ServiceToken,
                ["ObjectStorage:AutoCreateBucket"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IObjectStore>(ObjectStore);
        });
    }
}
