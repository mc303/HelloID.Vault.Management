namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Specifies the type of database backend to use.
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// SQLite database (local file-based database).
    /// </summary>
    Sqlite,

    /// <summary>
    /// PostgreSQL database (remote server, e.g., Supabase).
    /// </summary>
    PostgreSql,

    /// <summary>
    /// Turso database (cloud-based SQLite-compatible via REST API).
    /// </summary>
    Turso
}
