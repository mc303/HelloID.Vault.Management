using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// CRUD implementation shared by all simple reference data repositories.
/// The SQL lives here once; backends only differ in how statements execute
/// (see <see cref="DapperReferenceDataRepositoryBase{TEntity}"/> and
/// <see cref="Turso.TursoReferenceDataRepositoryBase{TEntity}"/>).
/// </summary>
public abstract class ReferenceDataRepositoryBase<TEntity> : RepositoryExecutionBase
    where TEntity : class, IReferenceDataEntity
{
    protected abstract string TableName { get; }

    private string SelectSql => $"SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM {TableName}";

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await QueryRowsAsync<TEntity>($"{SelectSql} ORDER BY name").ConfigureAwait(false);
    }

    public virtual async Task<TEntity?> GetByIdAsync(string externalId, string source)
    {
        return await QueryFirstRowAsync<TEntity>(
            $"{SelectSql} WHERE external_id = @ExternalId AND source = @Source",
            new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }

    public virtual async Task<int> InsertAsync(TEntity entity)
    {
        return await ExecuteRowsAsync(
            $"INSERT INTO {TableName} (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
            entity).ConfigureAwait(false);
    }

    public virtual async Task<int> UpdateAsync(TEntity entity)
    {
        // Arguments are listed in placeholder-appearance order for positional
        // binding; @Source appears twice but is a single named parameter
        return await ExecuteRowsAsync(
            $"UPDATE {TableName} SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source",
            new { entity.Code, entity.Name, entity.Source, entity.ExternalId }).ConfigureAwait(false);
    }

    public virtual async Task<int> DeleteAsync(string externalId, string source)
    {
        return await ExecuteRowsAsync(
            $"DELETE FROM {TableName} WHERE external_id = @ExternalId AND source = @Source",
            new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }
}
