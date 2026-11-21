using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Data.Repositories.Turso;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoOrganizationRepository : TursoReferenceDataRepositoryBase<Organization>, IOrganizationRepository
{
    public TursoOrganizationRepository(ITursoClient client) : base(client) { }

    protected override string TableName => "organizations";
}
