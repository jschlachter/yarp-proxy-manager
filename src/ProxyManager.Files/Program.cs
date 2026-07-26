using Serilog;
using Serilog.Events;

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

    builder.Services.AddFilesServices(builder.Configuration);
    builder.Services.AddHostedService<BucketBootstrapHostedService>();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

    var app = builder.Build();

    app.UseSerilogRequestLogging();

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
