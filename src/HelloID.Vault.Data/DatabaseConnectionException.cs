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
