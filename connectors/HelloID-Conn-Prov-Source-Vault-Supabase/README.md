# HelloID-Conn-Prov-Source-Vault-Supabase

HelloID source connector for Vault Management Supabase database.

## Version

| Version | Description | Date |
|---------|-------------|------|
| 2.0.0   | Added PostgREST embedding, field exclusion, configurable auth | 2026-02-17 |
| 1.0.0   | Initial release | 2026-02-16 |

## Description

This connector imports persons, contracts, and departments from a Vault Management Supabase PostgreSQL database into HelloID for identity provisioning using the Supabase REST API (PostgREST).

### Features (v2.0.0)

- **PostgREST Resource Embedding**: Fetches related data in single queries using foreign key relationships
- **Configurable Authentication**: Toggle Bearer token authentication on/off
- **Field Exclusion**: Remove unwanted fields from output (e.g., created_at, updated_at)
- **Advanced Error Handling**: Detailed HTTP error messages with debug context
- **Flattened Output**: Nested objects flattened with prefixed keys for easy mapping

## Prerequisites

- HelloID Provisioning agent (cloud or on-prem)
- Supabase project with Vault Management schema deployed
- API key with read access (service_role key recommended)

## Connection Settings

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| Supabase URL | Project URL (e.g., https://xyzcompany.supabase.co) | Yes | - |
| API Key | Supabase API key (service_role key for full access) | Yes | - |
| Use Authentication | Use Bearer token authentication | No | true |
| Include Inactive Contracts | Include contracts with end dates in the past | No | false |
| Fields to Exclude | Comma-separated list of fields to exclude (e.g., created_at,updated_at) | No | - |
| Debug Mode | Enable verbose logging for troubleshooting | No | false |

## Obtaining API Credentials

1. Log in to your Supabase dashboard
2. Navigate to Project Settings > API
3. Copy the **Project URL** (baseUrl)
4. Copy the **service_role** key (apiKey) - recommended for full access
   - Alternatively, use the **anon** key if Row Level Security is configured

## Database Schema Requirements

The connector expects the following tables with foreign key relationships:

- `persons` - Person records
- `contracts` - Contract records (FK to persons, departments, titles)
- `departments` - Department records
- `titles` - Title records

## API Endpoints Used

The connector uses the Supabase PostgREST API:

- `GET /rest/v1/persons` - Retrieve persons
- `GET /rest/v1/contracts` - Retrieve contracts with joins
- `GET /rest/v1/departments` - Retrieve departments

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
      "manager_external_id": "manager_001",
      "sequence_number": 1,
      "start_date": "2024-01-01",
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

## Security Notes

- The **service_role** key bypasses Row Level Security - protect it carefully
- Consider using **anon** key with proper RLS policies for production environments
- Store API keys securely in HelloID's encrypted configuration

## Getting Help

For more information on configuring HelloID PowerShell connectors, see the [HelloID documentation](https://docs.helloid.com/).
