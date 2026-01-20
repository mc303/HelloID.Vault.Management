using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services;

public class ContractService : IContractService
{
    private readonly IContractRepository _contractRepository;
    private readonly IPersonRepository _personRepository;

    public ContractService(IContractRepository contractRepository, IPersonRepository personRepository)
    {
        _contractRepository = contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
    }

    public async Task<Contract?> GetByIdAsync(int contractId)
    {
        System.Diagnostics.Debug.WriteLine($"[ContractService] GetByIdAsync called - ContractId: {contractId}");
        var contract = await _contractRepository.GetByIdAsync(contractId);
        System.Diagnostics.Debug.WriteLine($"[ContractService] GetByIdAsync result: {(contract != null ? "NULL" : $"ContractId={contract.ContractId}, ExternalId={contract.ExternalId}, LocationExternalId={contract.LocationExternalId}, LocationSource={contract.LocationSource}")}");
        return contract;
    }

    public async Task<(IEnumerable<ContractDetailDto> items, int totalCount)> GetPagedAsync(ContractFilter filter, int page, int pageSize)
    {
        return await _contractRepository.GetPagedDetailsAsync(filter, page, pageSize);
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllAsync()
    {
        return await _contractRepository.GetAllDetailsAsync();
    }

    public async Task<ContractJsonDto?> GetContractJsonByIdAsync(int contractId)
    {
        return await _contractRepository.GetJsonViewByIdAsync(contractId);
    }

    public async Task<int> SaveAsync(Contract contract)
    {
        await ValidateAsync(contract);

        System.Diagnostics.Debug.WriteLine($"[ContractService] SaveAsync START - ContractId: {contract.ContractId}, ExternalId: '{contract.ExternalId}', PersonId: '{contract.PersonId}'");
        System.Diagnostics.Debug.WriteLine($"[ContractService] Reference fields BEFORE save - LocationExternalId: '{contract.LocationExternalId}', LocationSource: '{contract.LocationSource}', DepartmentExternalId: '{contract.DepartmentExternalId}', TitleExternalId: '{contract.TitleExternalId}'");

        int contractId;
        if (contract.ContractId == 0)
        {
            contractId = await _contractRepository.InsertAsync(contract);
            System.Diagnostics.Debug.WriteLine($"[ContractService] Inserted new contract - ID: {contractId}");
        }
        else
        {
            await _contractRepository.UpdateAsync(contract);
            contractId = contract.ContractId;
            System.Diagnostics.Debug.WriteLine($"[ContractService] Updated existing contract - ID: {contractId}");

            // Rebuild cache AFTER save to ensure cache reflects new data
            await RebuildCacheAsync();
            System.Diagnostics.Debug.WriteLine($"[ContractService] Cache rebuild completed");
        }

        return contractId;
    }

    public async Task DeleteAsync(int contractId)
    {
        await _contractRepository.DeleteAsync(contractId);

        // Rebuild cache after delete
        await RebuildCacheAsync();
    }

    public async Task ValidateAsync(Contract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.PersonId))
            throw new ArgumentException("Person is required.");
            
        // Validate Person exists
        var person = await _personRepository.GetByIdAsync(contract.PersonId);
        if (person == null)
            throw new ArgumentException($"Person with ID '{contract.PersonId}' not found.");

        if (contract.Fte.HasValue && (contract.Fte < 0 || contract.Fte > 1.0))
            throw new ArgumentException("FTE must be between 0.0 and 1.0.");
            
        // Check dates logic? StartDate <= EndDate
        if (!string.IsNullOrEmpty(contract.StartDate) && !string.IsNullOrEmpty(contract.EndDate))
        {
            if (DateTime.TryParse(contract.StartDate, out var start) && DateTime.TryParse(contract.EndDate, out var end))
            {
                if (start > end)
                    throw new ArgumentException("Start Date cannot be after End Date.");
            }
        }
    }

    public async Task RefreshContractCacheItemAsync(int contractId)
    {
        await _contractRepository.RefreshContractCacheItemAsync(contractId);
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllFromCacheAsync()
    {
        return await _contractRepository.GetAllFromCacheAsync();
    }

    public async Task RebuildCacheAsync()
    {
        await _contractRepository.RebuildCacheAsync();
    }

    public async Task<CacheMetadata> GetCacheMetadataAsync()
    {
        return await _contractRepository.GetCacheMetadataAsync();
    }
}
