namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Complete DTO for displaying person details with primary contract and business contact.
/// Maps to person_details_view database view.
/// </summary>
public class PersonDetailDto
{
    // Person fields
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? UserName { get; set; }
    public string? Gender { get; set; }
    public string? HonorificPrefix { get; set; }
    public string? HonorificSuffix { get; set; }
    public string? BirthDate { get; set; }
    public string? BirthLocality { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Initials { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? FamilyNamePrefix { get; set; }
    public string? Convention { get; set; }
    public string? NickName { get; set; }
    public string? FamilyNamePartner { get; set; }
    public string? FamilyNamePartnerPrefix { get; set; }
    public bool Blocked { get; set; }
    public string? StatusReason { get; set; }
    public bool Excluded { get; set; }
    public bool HrExcluded { get; set; }
    public bool ManualExcluded { get; set; }
    public string? Source { get; set; }
    public string? SourceDisplayName { get; set; }

    // Primary contract fields
    public string? PrimaryContractId { get; set; }
    public string? PrimaryContractExternalId { get; set; }
    public string? PrimaryContractStartDate { get; set; }
    public string? PrimaryContractEndDate { get; set; }
    public string? PrimaryContractTypeCode { get; set; }
    public string? PrimaryContractTypeDescription { get; set; }
    public double? PrimaryContractFte { get; set; }
    public double? PrimaryContractHoursPerWeek { get; set; }
    public double? PrimaryContractPercentage { get; set; }
    public int? PrimaryContractSequence { get; set; }

    // Primary contract manager
    public string? PrimaryContractManagerId { get; set; }
    public string? PrimaryContractManagerName { get; set; }

    // Primary Manager (person-level)
    public string? PrimaryManagerPersonId { get; set; }
    public string? PrimaryManagerName { get; set; }
    public string? PrimaryManagerExternalId { get; set; }
    public string? PrimaryManagerSource { get; set; }
    public string? PrimaryManagerUpdatedAt { get; set; }

    // Primary contract - Location
    public string? PrimaryContractLocationId { get; set; }
    public string? PrimaryContractLocationName { get; set; }

    // Primary contract - Cost center
    public string? PrimaryContractCostCenterId { get; set; }
    public string? PrimaryContractCostCenterName { get; set; }

    // Primary contract - Cost bearer
    public string? PrimaryContractCostBearerId { get; set; }
    public string? PrimaryContractCostBearerName { get; set; }

    // Primary contract - Employer
    public string? PrimaryContractEmployerId { get; set; }
    public string? PrimaryContractEmployerName { get; set; }

    // Primary contract - Team
    public string? PrimaryContractTeamId { get; set; }
    public string? PrimaryContractTeamName { get; set; }

    // Primary contract - Department
    public string? PrimaryContractDepartmentId { get; set; }
    public string? PrimaryContractDepartmentName { get; set; }
    public string? PrimaryContractDepartmentCode { get; set; }

    // Primary contract - Division
    public string? PrimaryContractDivisionId { get; set; }
    public string? PrimaryContractDivisionName { get; set; }

    // Primary contract - Title
    public string? PrimaryContractTitleId { get; set; }
    public string? PrimaryContractTitleName { get; set; }

    // Primary contract - Organization
    public string? PrimaryContractOrganizationId { get; set; }
    public string? PrimaryContractOrganizationName { get; set; }

    // Business contact fields
    public int? PrimaryContactId { get; set; }
    public string? PrimaryContactType { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhoneMobile { get; set; }
    public string? PrimaryContactPhoneFixed { get; set; }
    public string? PrimaryContactAddressStreet { get; set; }
    public string? PrimaryContactAddressStreetExt { get; set; }
    public string? PrimaryContactAddressHouseNumber { get; set; }
    public string? PrimaryContactAddressHouseNumberExt { get; set; }
    public string? PrimaryContactAddressPostal { get; set; }
    public string? PrimaryContactAddressLocality { get; set; }
    public string? PrimaryContactAddressCountry { get; set; }

    // Calculated fields
    public string PersonStatus { get; set; } = "No Contract";
    public string? PrimaryContractDateRange { get; set; }
}
