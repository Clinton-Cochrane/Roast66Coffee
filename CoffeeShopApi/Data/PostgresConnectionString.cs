using Npgsql;

namespace CoffeeShopApi.Data;

/// <summary>
/// Normalizes portable PostgreSQL URLs and Npgsql connection strings into the
/// single application connection contract used by local, development, and
/// production environments.
/// </summary>
internal static class PostgresConnectionString
{
    internal const int MaximumPoolSize = 20;

    internal static string Build(string? configuredConnectionString)
    {
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured.");
        }

        var value = configuredConnectionString.Trim();
        var builder = IsPostgresUrl(value)
            ? FromPostgresUrl(value)
            : new NpgsqlConnectionStringBuilder(value);

        builder.Pooling = true;
        builder.MaxPoolSize = MaximumPoolSize;
        return builder.ConnectionString;
    }

    private static bool IsPostgresUrl(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static NpgsqlConnectionStringBuilder FromPostgresUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !IsPostgresUrl(value) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("The PostgreSQL URL is invalid.", nameof(value));
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (credentials.Length == 0 ||
            string.IsNullOrWhiteSpace(credentials[0]) ||
            string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException(
                "The PostgreSQL URL must include a username and database.",
                nameof(value));
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length == 2
                ? Uri.UnescapeDataString(credentials[1])
                : string.Empty
        };

        foreach (var pair in uri.Query.TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var queryValue = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : string.Empty;
            builder[key] = queryValue;
        }

        return builder;
    }
}
