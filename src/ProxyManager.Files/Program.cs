using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

using Serilog;
using Serilog.Events;

using Wolverine;
using Wolverine.RabbitMQ;

using West94.ProxyManager.Files.Auth;
using West94.ProxyManager.Files.Endpoints;
using West94.ProxyManager.Files.Infrastructure;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Data;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<ServiceTokenOptions>(builder.Configuration.GetSection(ServiceTokenOptions.Section));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.Audience = builder.Configuration["Authentication:Audience"];
        })
        .AddScheme<AuthenticationSchemeOptions, ServiceTokenAuthenticationHandler>(
            ServiceTokenAuthenticationHandler.SchemeName, _ => { });

    // Browser calls arrive on the JWT bearer scheme; the API's service-to-service client arrives
    // on ServiceToken. RequireAuthorization() (no scheme args) uses this default policy, so either
    // succeeding is sufficient.
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme, ServiceTokenAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .Build();
    });

    builder.Services.AddFilesServices(builder.Configuration);
    builder.Services.AddHostedService<FilesDatabaseMigrationService>();
    builder.Services.AddHostedService<BucketBootstrapHostedService>();
    builder.Services.AddHostedService<StagedAssetSweeper>();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

    var rabbitEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled", defaultValue: true);

    builder.Host.UseWolverine(opts =>
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<FilesDbContext>();


        if (rabbitEnabled)
        {
            opts.AddRabbitMqTransport(builder.Configuration)
                .AutoProvision()
                .DeclareExchange("certificates", exchange =>
                {
                    exchange.ExchangeType = ExchangeType.Fanout;
                    exchange.IsDurable = true;
                    exchange.BindQueue("files-certificate-cleanup", bindingKey: string.Empty);
                });

            opts.ListenToRabbitQueue("files-certificate-cleanup");
        }
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapFileEndpoints();

    Log.Information("Starting Proxy Manager Files host...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
