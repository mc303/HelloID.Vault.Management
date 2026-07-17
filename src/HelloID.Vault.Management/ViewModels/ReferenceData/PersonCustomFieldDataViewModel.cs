using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

public class PersonCustomFieldDataViewModel : CustomFieldDataViewModelBase
{
    public override string TableName => "persons";
    public override string TableDisplayName => "Person Fields";

    public override List<(string FieldName, string DisplayName, double Width)> GetBaseColumns() =>
        new()
        {
            ("display_name", "Display Name", 200),
            ("external_id", "External ID", 150)
        };

    public override List<(string FieldName, string DisplayName)> GetBaseSearchFields() =>
        new()
        {
            ("display_name", "Display Name"),
            ("external_id", "External ID")
        };

    public PersonCustomFieldDataViewModel(ICustomFieldRepository customFieldRepository)
        : base(customFieldRepository)
    {
    }
}
