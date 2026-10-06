namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Extension methods for <see cref="DatabaseType"/>.
/// </summary>
public static class DatabaseTypeExtensions
{
    /// <summary>
    /// Determines whether the database type is a PostgreSQL backend
    /// (either Supabase-hosted or a standard self-hosted server).
    /// </summary>
    /// <param name="databaseType">The database type to check.</param>
    /// <returns>True if the type uses the PostgreSQL (Npgsql) backend.</returns>
    public static bool IsPostgreSqlBackend(this DatabaseType databaseType)
    {
        return databaseType == DatabaseType.PostgreSql ||
               databaseType == DatabaseType.PostgreSqlStandard;
    }
}
