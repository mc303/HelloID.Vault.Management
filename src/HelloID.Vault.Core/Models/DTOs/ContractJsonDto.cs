namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying contract details in the exact structure as vault.json
/// </summary>
public class ContractJsonDto
{
    public Context Context { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public ContractType Type { get; set; } = new();
    public ContractDetails Details { get; set; } = new();
    public Location Location { get; set; } = new();
    public CostCenter CostCenter { get; set; } = new();
    public CostBearer CostBearer { get; set; } = new();
    public Employer Employer { get; set; } = new();
    public Manager Manager { get; set; } = new();
    public Team Team { get; set; } = new();
    public ContractDepartment Department { get; set; } = new();
    public Division Division { get; set; } = new();
    public Title Title { get; set; } = new();
    public Organization Organization { get; set; } = new();
}

public class Context
{
    public bool InConditions { get; set; } = false;
}

public class ContractType
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public class ContractDetails
{
    public double? Fte { get; set; }
    public double? HoursPerWeek { get; set; }
    public double? Percentage { get; set; }
    public int? Sequence { get; set; }
}

public class Location
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class CostCenter
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class CostBearer
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class Employer
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class Manager
{
    public string? PersonId { get; set; }
    public string? ExternalId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public class Team
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class ContractDepartment
{
    public string? DisplayName { get; set; }
    public string? ExternalId { get; set; }
}

public class Division
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class Title
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class Organization
{
    public string? ExternalId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}