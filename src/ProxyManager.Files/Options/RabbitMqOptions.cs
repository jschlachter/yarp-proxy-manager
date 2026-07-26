namespace West94.ProxyManager.Files.Options;

/// <summary>
/// Duplicated from <c>West94.ProxyManager.API.Options.RabbitMqOptions</c> — Files does not
/// reference the API project, so this small binding class is copied rather than shared.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string Section = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>Set to false to skip RabbitMQ transport setup (used in testing).</summary>
    public bool Enabled { get; set; } = true;
}
