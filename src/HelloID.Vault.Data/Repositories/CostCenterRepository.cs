using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class CostCenterRepository : DapperReferenceDataRepositoryBase<CostCenter>, ICostCenterRepository
{
    public CostCenterRepository(IDatabaseConnectionFactory connectionFactory) : base(connectionFactory) { }

    protected override string TableName => "cost_centers";
}
