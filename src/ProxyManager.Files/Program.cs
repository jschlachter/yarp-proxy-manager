using Microsoft.AspNetCore.Authentication.JwtBearer;

using Serilog;
using Serilog.Events;

using West94.ProxyManager.Files.Endpoints;
using West94.ProxyManager.Files.Infrastructure;
using West94.ProxyManager.Files.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.Audience = builder.Configuration["Authentication:Audience"];
        });

    builder.Services.AddAuthorization();

    builder.Services.AddFilesServices(builder.Configuration);
    builder.Services.AddHostedService<FilesDatabaseMigrationService>();
    builder.Services.AddHostedService<BucketBootstrapHostedService>();
    builder.Services.AddHostedService<StagedAssetSweeper>();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

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
