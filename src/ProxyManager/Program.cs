using Scalar.AspNetCore;

using Serilog;
using Serilog.Events;

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
        .LoadFromConfig(configuration.GetSection("ReverseProxy"));

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
    app.MapReverseProxy();

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