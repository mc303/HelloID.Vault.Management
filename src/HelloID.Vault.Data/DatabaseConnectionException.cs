namespace HelloID.Vault.Data;

/// <summary>
/// Thrown when the database server cannot be reached (DNS failure, refused
/// connection, timeout). Lets the application distinguish connectivity
/// problems from database/schema errors and start in offline mode so the
/// user can change database settings.
/// </summary>
public class DatabaseConnectionException : Exception
{
    public DatabaseConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when the state of the database schema cannot be verified. Initialization
/// must abort instead of treating the failure as "schema missing" and rebuilding
/// over live data.
/// </summary>
public class DatabaseSchemaException : Exception
{
    public DatabaseSchemaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
