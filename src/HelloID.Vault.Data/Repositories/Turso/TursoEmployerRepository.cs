using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoEmployerRepository : IEmployerRepository
{
    private readonly ITursoClient _client;

    public TursoEmployerRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Employer>> GetAllAsync()
    {
        Debug.WriteLine("[TursoEmployerRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM employers ORDER BY name";
        var result = await _client.QueryAsync<Employer>(sql);
        return result.Rows;
    }

    public async Task<Employer?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoEmployerRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM employers WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Employer>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Employer employer)
    {
        Debug.WriteLine($"[TursoEmployerRepository] InsertAsync: {employer.ExternalId}");
        var sql = "INSERT INTO employers (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, employer);
    }

    public async Task<int> UpdateAsync(Employer employer)
    {
        Debug.WriteLine($"[TursoEmployerRepository] UpdateAsync: {employer.ExternalId}");
        var sql = "UPDATE employers SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { employer.Code, employer.Name, employer.Source, employer.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoEmployerRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM employers WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
