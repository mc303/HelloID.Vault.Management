namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Execution seam shared by repository base classes: the SQL lives in the
/// abstract repository, and each backend implements only these primitives
/// (Dapper on a connection, or the Turso HTTP pipeline).
/// Placeholders use @Name syntax: Dapper binds by name, Turso/sqld binds by
/// the parameter index assigned at first appearance - parameter objects must
/// list properties in placeholder order.
/// </summary>
public abstract class RepositoryExecutionBase
{
    protected abstract Task<IEnumerable<TResult>> QueryRowsAsync<TResult>(string sql, object? parameters = null)
        where TResult : class;

    protected abstract Task<TResult?> QueryFirstRowAsync<TResult>(string sql, object? parameters = null)
        where TResult : class;

    protected abstract Task<int> ExecuteRowsAsync(string sql, object? parameters = null);

    protected abstract Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null);
}
