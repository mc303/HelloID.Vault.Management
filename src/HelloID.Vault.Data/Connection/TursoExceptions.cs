namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Base exception for all Turso-related errors.
/// </summary>
public class TursoException : Exception
{
    public string? ErrorCode { get; set; }
    public string? ErrorType { get; set; }

    public TursoException(string message) : base(message)
    {
    }

    public TursoException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public TursoException(string message, string? errorCode, string? errorType) : base(message)
    {
        ErrorCode = errorCode;
        ErrorType = errorType;
    }
}

/// <summary>
/// Exception thrown when network connectivity issues occur.
/// </summary>
public class TursoNetworkException : TursoException
{
    public bool IsOffline { get; set; }

    public TursoNetworkException(string message, bool isOffline = false) : base(message)
    {
        IsOffline = isOffline;
    }

    public TursoNetworkException(string message, Exception innerException, bool isOffline = false) 
        : base(message, innerException)
    {
        IsOffline = isOffline;
    }
}

/// <summary>
/// Exception thrown when connection to Turso fails.
/// </summary>
public class TursoConnectionException : TursoException
{
    public int? StatusCode { get; set; }

    public TursoConnectionException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }

    public TursoConnectionException(string message, Exception innerException, int? statusCode = null) 
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class TursoAuthException : TursoException
{
    public bool IsTokenExpired { get; set; }
    public bool IsTokenInvalid { get; set; }

    public TursoAuthException(string message, bool isTokenExpired = false, bool isTokenInvalid = false) 
        : base(message)
    {
        IsTokenExpired = isTokenExpired;
        IsTokenInvalid = isTokenInvalid;
    }
}

/// <summary>
/// Exception thrown when a SQL query fails.
/// </summary>
public class TursoQueryException : TursoException
{
    public string? Sql { get; set; }

    public TursoQueryException(string message, string? sql = null) : base(message)
    {
        Sql = sql;
    }

    public TursoQueryException(string message, string? errorCode, string? errorType, string? sql = null) 
        : base(message, errorCode, errorType)
    {
        Sql = sql;
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class TursoRateLimitException : TursoException
{
    public int RetryAfterSeconds { get; set; }

    public TursoRateLimitException(string message, int retryAfterSeconds = 60) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Exception thrown when token refresh fails.
/// </summary>
public class TursoTokenRefreshException : TursoException
{
    public TursoTokenRefreshException(string message) : base(message)
    {
    }

    public TursoTokenRefreshException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when database schema is incompatible.
/// </summary>
public class TursoSchemaException : TursoException
{
    public TursoSchemaException(string message) : base(message)
    {
    }

    public TursoSchemaException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
