using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

using Serilog;
using Serilog.Events;

using Wolverine;
using Wolverine.RabbitMQ;

using West94.ProxyManager.API.Infrastructure;
using West94.ProxyManager.API.Infrastructure.Files;
using West94.ProxyManager.API.Options;
using West94.ProxyManager.API.Services;
using West94.ProxyManager.Core.Messages.Events;
using West94.ProxyManager.Endpoints;
using West94.ProxyManager.Infrastructure.Data;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddProxyManagerOpenApi();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.Audience = builder.Configuration["Authentication:Audience"];
        });

    builder.Services.AddAuthorization();

    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.Section));

    builder.Services.AddProxyManagerServices(builder.Configuration);
    builder.Services.AddHostedService<DatabaseMigrationService>();

    builder.Services.Configure<FilesServiceOptions>(builder.Configuration.GetSection(FilesServiceOptions.Section));
    builder.Services.AddHttpClient<IFileAssetClient, FileAssetClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<FilesServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add("X-Files-Service-Token", options.ServiceToken);
    });
    builder.Services.AddHostedService<CertificateAssetReconciliationService>();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

    var rabbitEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled", defaultValue: true);

    builder.Host.UseWolverine(opts =>
    {
        // TODO: https://wolverinefx.net/guide/migration#:~:text=the%20using%20directive.-,ServiceLocationPolicy,-.NotAllowed%20is%20the
        // This is required to allow Wolverine to resolve the DbContext from DI when publishing messages.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ProxyManagerDbContext>();
        
        if (rabbitEnabled)
        {
            opts.AddRabbitMqTransport(builder.Configuration)
                .AutoProvision()
                .DeclareExchange("proxy-hosts", exchange =>
                {
                    exchange.ExchangeType = ExchangeType.Fanout;
                    exchange.IsDurable = true;
                })
                .DeclareExchange("certificates", exchange =>
                {
                    exchange.ExchangeType = ExchangeType.Fanout;
                    exchange.IsDurable = true;
                });

            opts.PublishMessage<ProxyHostCreatedEvent>().ToRabbitExchange("proxy-hosts");
            opts.PublishMessage<ProxyHostUpdatedEvent>().ToRabbitExchange("proxy-hosts");
            opts.PublishMessage<ProxyHostDeletedEvent>().ToRabbitExchange("proxy-hosts");

            opts.PublishMessage<CertificateCreatedEvent>().ToRabbitExchange("certificates");
            opts.PublishMessage<CertificateUpdatedEvent>().ToRabbitExchange("certificates");
            opts.PublishMessage<CertificateDeletedEvent>().ToRabbitExchange("certificates");
        }
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options
                .SortOperationsByMethod()
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithTitle("Proxy Manager API")
                .WithTheme(ScalarTheme.Mars)
                .AddPreferredSecuritySchemes(["Bearer"])
                .AddHttpAuthentication("Bearer", scheme => { });
        });
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapProxyHostEndpoints();
    app.MapCertificateEndpoints();

    Log.Information("Starting Proxy Manager API host...");
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
