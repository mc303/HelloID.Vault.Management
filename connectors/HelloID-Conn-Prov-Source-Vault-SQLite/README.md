# HelloID-Conn-Prov-Source-Vault-SQLite

HelloID source connector for Vault Management SQLite database.

## Version

| Version | Description | Date |
|---------|-------------|------|
| 2.0.0   | Switched to Microsoft.Data.Sqlite, added field exclusion, advanced error handling, contacts support | 2026-02-17 |
| 1.0.0   | Initial release | 2026-02-16 |

## Description

This connector imports persons, contracts, and departments from a Vault Management SQLite database into HelloID for identity provisioning.

### Features (v2.0.0)

- **Field Exclusion**: Remove unwanted fields from output (e.g., created_at, updated_at)
- **Advanced Error Handling**: Detailed error messages with debug context
- **Flattened Output**: Nested objects flattened with prefixed keys for easy mapping
- **Contacts Support**: Business and personal contacts with type-based prefixed keys
- **Extended Contract Data**: All lookup tables (locations, cost_centers, employers, teams, divisions, organizations)

## Prerequisites

- HelloID Provisioning agent (cloud or on-prem)
- Microsoft.Data.Sqlite assembly with SQLitePCLRaw bundles on the agent machine
- Read access to the SQLite database file

### Required DLLs

The connector requires the following assemblies (available from [NuGet](https://www.nuget.org/packages/Microsoft.Data.Sqlite)):

| DLL | Description |
|-----|-------------|
| Microsoft.Data.Sqlite.dll | Main ADO.NET provider |
| SQLitePCLRaw.batteries_v2.dll | Battery bundle for SQLite |
| SQLitePCLRaw.core.dll | Core SQLite PCL library |
| SQLitePCLRaw.provider.e_sqlite3.dll | Native SQLite provider |

**Installation:**

1. Download the NuGet package: https://www.nuget.org/api/v2/package/Microsoft.Data.Sqlite/8.0.0
2. Extract the .nupkg file (it's a ZIP archive)
3. Create a folder for the DLLs (e.g., `C:\ProgramData\HelloID\Connectors\SQLite\`)
4. Copy ALL of the following DLLs to that folder:
   - From `lib/netstandard2.0/`: `Microsoft.Data.Sqlite.dll`
   - From the package root or dependencies: `SQLitePCLRaw.batteries_v2.dll`, `SQLitePCLRaw.core.dll`, `SQLitePCLRaw.provider.e_sqlite3.dll`
5. Configure the `SQLite DLL Path` setting to point to the full path of `Microsoft.Data.Sqlite.dll`

**Important:** All four DLLs must be in the same folder. The connector will automatically load the dependencies from the same directory.

## Connection Settings

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| Database Path | Full path to the Vault SQLite database file | Yes | - |
| SQLite DLL Path | Full path to Microsoft.Data.Sqlite.dll (all dependencies must be in same folder) | Yes | - |
| Source System Filter | Filter data by source system (leave empty for all) | No | - |
| Include Inactive Contracts | Include contracts with end dates in the past | No | false |
| Fields to Exclude | Comma-separated list of fields to exclude (supports wildcards) | No | - |
| Debug Mode | Enable verbose logging for troubleshooting | No | false |

## Database Schema Requirements

The connector expects the following tables:

- `persons` - Person records with external_id, display_name, first_name, last_name, email
- `contracts` - Contract records with person_external_id, department_external_id, title_external_id, start_date, end_date
- `departments` - Department records with external_id, display_name, name, manager_person_external_id
- `titles` - Title records with external_id, name
- `contacts` - Contact records with person_external_id, type, email, phone, mobile
- `locations`, `cost_centers`, `cost_bearers`, `employers`, `teams`, `divisions`, `organizations` - Optional lookup tables

## Output

### Persons (persons.ps1)

The connector outputs flattened person objects with embedded contract data. Nested objects are flattened with prefixed keys:

```json
{
  "DisplayName": "John Doe",
  "ExternalId": "person_external_id",
  "Email": "john@example.com",
  "FirstName": "John",
  "LastName": "Doe",
  "contact_business_email": "john.work@company.com",
  "contact_personal_phone": "+31-6-12345678",
  "Contracts": [
    {
      "department_display_name": "Information Technology",
      "department_external_id": "IT001",
      "division_name": "Technology",
      "end_date": null,
      "external_id": "contract_001",
      "location_name": "Amsterdam",
      "manager_display_name": "Jane Smith",
      "manager_person_external_id": "manager_001",
      "sequence_number": "1",
      "start_date": "2024-01-01T00:00:00Z",
      "title_name": "Developer"
    }
  ]
}
```

### Departments (departments.ps1)

```json
{
  "DisplayName": "Information Technology",
  "ExternalId": "IT001",
  "ManagerExternalId": "manager_001",
  "ParentExternalId": "COMPANY"
}
```

## Getting Help

For more information on configuring HelloID PowerShell connectors, see the [HelloID documentation](https://docs.helloid.com/).
