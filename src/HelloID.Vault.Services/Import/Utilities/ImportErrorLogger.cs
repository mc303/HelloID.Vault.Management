using System.Diagnostics;
using Npgsql;

namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// Provides error logging and user-friendly message creation for import operations.
/// </summary>
public static class ImportErrorLogger
{
    /// <summary>
    /// Logs detailed error information for database exceptions.
    /// </summary>
    public static void LogDatabaseException(Exception ex, string operation, string? context = null)
    {
        Debug.WriteLine($"[Import Error] Operation: {operation}");
        if (!string.IsNullOrEmpty(context))
        {
            Debug.WriteLine($"[Import Error] Context: {context}");
        }
        Debug.WriteLine($"[Import Error] Exception Type: {ex.GetType().Name}");
        Debug.WriteLine($"[Import Error] Message: {ex.Message}");

        if (ex is PostgresException pgEx)
        {
            Debug.WriteLine($"[Import Error] PostgreSQL Error Code: {pgEx.SqlState}");
            Debug.WriteLine($"[Import Error] PostgreSQL Position: {pgEx.Position}");
            Debug.WriteLine($"[Import Error] PostgreSQL Hint: {pgEx.Hint}");

            if (pgEx.Data.Count > 0)
            {
                Debug.WriteLine($"[Import Error] PostgreSQL Data:");
                foreach (var key in pgEx.Data.Keys)
                {
                    Debug.WriteLine($"[Import Error]   {key}: {pgEx.Data[key]}");
                }
            }
        }

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            var stackTrace = ex.StackTrace.Split('\n').Take(5);
            Debug.WriteLine($"[Import Error] Stack Trace (first 5 lines):");
            foreach (var line in stackTrace)
            {
                Debug.WriteLine($"[Import Error]   {line.Trim()}");
            }
        }

        if (ex.InnerException != null)
        {
            Debug.WriteLine($"[Import Error] Inner Exception: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    /// Creates a user-friendly error message from a database exception.
    /// </summary>
    public static string CreateUserErrorMessage(Exception ex, string operation)
    {
        if (ex is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                "42883" => $"Import failed: {pgEx.Message}\n\nThis is likely a database compatibility issue. Please check the logs for details.",
                "23505" => $"Import failed: A duplicate key constraint was violated.\n\n{pgEx.Message}",
                "23503" => $"Import failed: A foreign key constraint was violated.\n\n{pgEx.Message}",
                "22008" => $"Import failed: Invalid date/time format.\n\n{pgEx.Message}",
                "08001" => $"Import failed: Cannot connect to database server.\n\n{pgEx.Message}",
                "3D000" => $"Import failed: Invalid catalog name in connection string.\n\n{pgEx.Message}",
                _ => $"Import failed: {pgEx.Message}\n\nSQL State: {pgEx.SqlState}"
            };
        }

        return $"Import failed: {ex.Message}\n\nOperation: {operation}";
    }
}
