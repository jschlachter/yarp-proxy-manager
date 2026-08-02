using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

using West94.ProxyManager.Files.Options;

namespace West94.ProxyManager.Files.Infrastructure;

/// <summary>Duplicated from ProxyManager.API's extension of the same name — Files does not reference the API project.</summary>
public static class WolverineOptionsExtensions
{
    public static RabbitMqTransportExpression AddRabbitMqTransport(this WolverineOptions opts, IConfiguration configuration)
    {
        

        var section = configuration.GetSection(RabbitMqOptions.Section);
        var options = section.Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{RabbitMqOptions.Section}' for RabbitMQ transport setup.");

        return opts.UseRabbitMq(rabbit =>
        {
            rabbit.HostName = options.Host;
            if (options.UserName is not null) rabbit.UserName = options.UserName;
            if (options.Password is not null) rabbit.Password = options.Password;
        });
    }
}
