using System.Text.RegularExpressions;

namespace West94.ProxyManager.Core.Configuration;

/// <summary>
/// Replaces <c>{{Token}}</c> placeholders in a configuration value (typically a connection string)
/// with values looked up by token name.
/// </summary>
/// <remarks>
/// Lets a checked-in or unit-file connection string keep its secret out of band: the template
/// carries <c>Password={{POSTGRES_PASSWORD}}</c> and the value arrives separately, e.g. as a Podman
/// secret exposed as the <c>POSTGRES_PASSWORD</c> environment variable. It lives in Core and takes a
/// lookup delegate instead of <c>IConfiguration</c> so both ProxyManager.Infrastructure and the
/// standalone ProxyManager.Files service can share it without extra package references.
/// </remarks>
/// <example>
/// <code>
/// var cs = ConfigurationTokens.Interpolate(
///     "Host=db;Password={{POSTGRES_PASSWORD}}", key => configuration[key], out var missing);
/// </code>
/// </example>
public static partial class ConfigurationTokens
{
    /// <summary>Matches <c>{{NAME}}</c> where NAME is letters, digits and underscores.</summary>
    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\}\}")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// Replaces every <c>{{Token}}</c> in <paramref name="template"/> with <paramref name="lookup"/>(Token).
    /// </summary>
    /// <param name="template">The value containing zero or more placeholders.</param>
    /// <param name="lookup">Returns the value for a token name, or <see langword="null"/> when it is not set.</param>
    /// <param name="missing">
    /// Distinct token names the lookup returned <see langword="null"/> for, in order of first appearance.
    /// Those placeholders are left in the result unchanged so the failure is visible downstream.
    /// </param>
    /// <returns>The interpolated value.</returns>
    public static string Interpolate(string template, Func<string, string?> lookup, out IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(lookup);

        var unresolved = new List<string>();

        var result = TokenPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (lookup(key) is { } value)
            {
                return value;
            }

            if (!unresolved.Contains(key))
            {
                unresolved.Add(key);
            }

            return match.Value;
        });

        missing = unresolved;

        return result;
    }
}
