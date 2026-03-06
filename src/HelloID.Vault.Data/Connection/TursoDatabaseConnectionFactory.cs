using System.Data;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Data;

public class TursoDatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly ITursoClient _tursoClient;

    public DatabaseType DatabaseType => DatabaseType.Turso;

    public TursoDatabaseConnectionFactory(ITursoClient tursoClient)
    {
        _tursoClient = tursoClient ?? throw new ArgumentNullException(nameof(tursoClient));
    }

    public IDbConnection CreateConnection()
    {
        throw new NotSupportedException(
            "Direct database connections are not supported for Turso. " +
            "Use ITursoClient via repositories instead.");
    }

    public IDbConnection CreateConnection(bool enforceForeignKeys)
    {
        throw new NotSupportedException(
            "Direct database connections are not supported for Turso. " +
            "Use ITursoClient via repositories instead.");
    }

    public ITursoClient GetTursoClient() => _tursoClient;
}
