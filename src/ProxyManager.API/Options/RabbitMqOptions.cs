namespace West94.ProxyManager.API.Options;

public sealed class RabbitMqOptions
{
    public const string Section = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>Set to false to skip RabbitMQ transport setup (used in testing).</summary>
    public bool Enabled { get; set; } = true;
}
