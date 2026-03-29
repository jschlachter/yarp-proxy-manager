using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

using West94.ProxyManager.API.Options;

namespace West94.ProxyManager.API.Infrastructure;

public static class WolverineOptionsExtensions
{
    /// <summary>
    /// Configures the Wolverine RabbitMQ transport using a raw AMPQ connection string
    /// (e.g. <c>amqp://user:pass@host:5672/vhost</c>).
    /// </summary>
    public static RabbitMqTransportExpression AddRabbitMqTransport(
        this WolverineOptions opts, string amqpConnectionString) =>
        opts.UseRabbitMq(new Uri(amqpConnectionString));

    /// <summary>
    /// Configures the Wolverine RabbitMQ transport from <paramref name="configuration"/>.
    /// <c>RabbitMQ</c> section (<see cref="RabbitMqOptions"/>).
    /// </summary>
    public static RabbitMqTransportExpression AddRabbitMqTransport(
        this WolverineOptions opts, IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.Section);

        if (section is null)
        {
            throw new InvalidOperationException(
                $"Missing configuration section '{RabbitMqOptions.Section}' for RabbitMQ transport setup.");
        }

        var options = section.Get<RabbitMqOptions>()!;
        return opts.UseRabbitMq(rabbit =>
        {
            rabbit.HostName = options.Host;
            if (options.UserName is not null) rabbit.UserName = options.UserName;
            if (options.Password is not null) rabbit.Password = options.Password;
        });
    }
}
