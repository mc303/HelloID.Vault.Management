using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoTeamRepository : ITeamRepository
{
    private readonly ITursoClient _client;

    public TursoTeamRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Team>> GetAllAsync()
    {
        Debug.WriteLine("[TursoTeamRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM teams ORDER BY name";
        var result = await _client.QueryAsync<Team>(sql);
        return result.Rows;
    }

    public async Task<Team?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoTeamRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM teams WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Team>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Team team)
    {
        Debug.WriteLine($"[TursoTeamRepository] InsertAsync: {team.ExternalId}");
        var sql = "INSERT INTO teams (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, team);
    }

    public async Task<int> UpdateAsync(Team team)
    {
        Debug.WriteLine($"[TursoTeamRepository] UpdateAsync: {team.ExternalId}");
        var sql = "UPDATE teams SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { team.Code, team.Name, team.Source, team.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoTeamRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM teams WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
