using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;

using Serilog;
using Serilog.Events;

using Wolverine;
using Wolverine.RabbitMQ;

using West94.ProxyManager.API.Infrastructure;
using West94.ProxyManager.API.Options;
using West94.ProxyManager.Core.Messages.Events;
using West94.ProxyManager.Endpoints;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddOpenApi();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.Audience = builder.Configuration["Authentication:Audience"];
        });

    builder.Services.AddAuthorization();

    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.Section));

    builder.Services.AddProxyManagerServices();

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services));

    var rabbitEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled", defaultValue: true);

    builder.Host.UseWolverine(opts =>
    {
        if (rabbitEnabled)
        {
            var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:UserName");
            var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password");

            opts.UseRabbitMq(rabbit =>
            {
                rabbit.HostName = rabbitHost;
                if (rabbitUser is not null) rabbit.UserName = rabbitUser;
                if (rabbitPass is not null) rabbit.Password = rabbitPass;
            })
                .AutoProvision()
                .DeclareExchange("proxy-hosts", exchange =>
                {
                    exchange.ExchangeType = ExchangeType.Fanout;
                    exchange.IsDurable = true;
                });

            opts.PublishMessage<ProxyHostCreatedEvent>().ToRabbitExchange("proxy-hosts");
            opts.PublishMessage<ProxyHostUpdatedEvent>().ToRabbitExchange("proxy-hosts");
            opts.PublishMessage<ProxyHostDeletedEvent>().ToRabbitExchange("proxy-hosts");
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
                .WithTheme(ScalarTheme.Mars);
        });
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapProxyHostEndpoints();

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
