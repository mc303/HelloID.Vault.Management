using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

public class ContractCustomFieldDataViewModel : CustomFieldDataViewModelBase
{
    public override string TableName => "contracts";
    public override string TableDisplayName => "Contract Fields";

    public override List<(string FieldName, string DisplayName, double Width)> GetBaseColumns() =>
        new()
        {
            ("person_name", "Person", 200),
            ("external_id", "Contract External ID", 200)
        };

    public override List<(string FieldName, string DisplayName)> GetBaseSearchFields() =>
        new()
        {
            ("external_id", "Contract External ID"),
            ("person_name", "Person")
        };

    public ContractCustomFieldDataViewModel(ICustomFieldRepository customFieldRepository)
        : base(customFieldRepository)
    {
    }
}
