# HelloID-Conn-Prov-Source-Vault-Postgres-Npgsql

HelloID source connector for Vault Management PostgreSQL database using a .NET Framework wrapper DLL.

**📖 [Installation Guide](INSTALL.md)** - Step-by-step build and deploy instructions

## Version History

| Version | Description | Date |
|---------|-------------|------|
| 0.6.0 | Uses HelloID.PostgreSQL wrapper DLL (single-file deployment) | 2026-02-18 |
| 0.5.0 | Assembly resolution for PowerShell 5.1 | 2026-02-18 |
| 0.1.0 | Initial release | 2026-02-16 |

## Overview

This connector imports persons, contracts, and departments from a Vault Management PostgreSQL database into HelloID. It uses a custom .NET Framework 4.7.2 wrapper DLL that embeds Npgsql internally, eliminating PowerShell 5.1 assembly loading issues.

### Why the Wrapper DLL?

PowerShell 5.1 (default on Windows Server) cannot properly load .NET Standard assemblies like Npgsql. The wrapper DLL:

- Targets .NET Framework 4.7.2 (native to PowerShell 5.1)
- Embeds Npgsql 8.0.8 and all dependencies internally
- Handles assembly resolution automatically
- Provides a simple, clean API via `[HelloID.PostgreSQL.Query]::Execute()`

### Features

- **Single DLL deployment** - Only `HelloID.PostgreSQL.dll` needed
- **PowerShell 5.1 compatible** - Works on Windows Server 2016+
- **Flattened output** - All fields at root level with prefixed keys
- **Field exclusion** - Remove unwanted fields from output
- **Source filtering** - Import only data from specific source systems
- **Contract filtering** - Optionally include/exclude expired contracts

## Quick Start

1. **Build the wrapper DLL** (Windows required):
   ```powershell
   cd src\HelloID.PostgreSQL
   .\build.ps1
   ```

2. **Deploy to HelloID server**:
   ```
   Copy bin\Release\HelloID.PostgreSQL.dll to C:\HelloID\SourceData\
   ```

3. **Configure connector** in HelloID:
   - Set Wrapper DLL Path to `C:\HelloID\SourceData\HelloID.PostgreSQL.dll`
   - Enter PostgreSQL connection details

See [INSTALL.md](INSTALL.md) for detailed instructions.

## Configuration

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| Wrapper DLL Path | Full path to HelloID.PostgreSQL.dll | Yes | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| Host | PostgreSQL server hostname | Yes | `db-project.aivencloud.com` |
| Port | PostgreSQL port | Yes | `5432` or `25980` (Aiven) |
| Database | Database name | Yes | `defaultdb` |
| Username | Database username | Yes | `avnadmin` |
| Password | Database password | Yes | - |
| Source System Filter | Filter by source system | No | `HR-System-A` |
| Include Inactive Contracts | Include expired contracts | No | `false` |
| Fields to Exclude | Comma-separated fields to remove | No | `Source,custom_*` |
| Debug Mode | Enable verbose logging | No | `false` |

### Connection Examples

**Aiven PostgreSQL:**
| Setting | Value |
|---------|-------|
| Host | `db-your-project.aivencloud.com` |
| Port | `25980` |
| Database | `defaultdb` |
| Username | `avnadmin` |

**Local PostgreSQL:**
| Setting | Value |
|---------|-------|
| Host | `localhost` |
| Port | `5432` |
| Database | `vault` |
| Username | `postgres` |

## Output Format

### Persons (persons.ps1)

Returns person records with nested contracts array:

```json
{
  "DisplayName": "John Doe",
  "ExternalId": "person-001",
  "blocked": false,
  "birth_date": "1990-01-15",
  "contact_business_email": "john@company.com",
  "contact_personal_phone_mobile": "+31-6-12345678",
  "excluded": false,
  "family_name": "Doe",
  "gender": "M",
  "given_name": "John",
  "nick_name": "Johnny",
  "source": "HR-System-A",
  "user_name": "jdoe",
  "Contracts": [
    {
      "contract_external_id": "contract-001",
      "contract_start_date": "2024-01-01",
      "contract_end_date": "2025-12-31",
      "contract_sequence": 1,
      "contract_type_code": "PERM",
      "contract_fte": 1.0,
      "department_display_name": "Information Technology",
      "department_code": "IT001",
      "location_name": "Amsterdam",
      "manager_display_name": "Jane Smith",
      "manager_external_id": "manager-001",
      "title_name": "Senior Developer"
    }
  ]
}
```

**Person fields include:**
- Core: `DisplayName`, `ExternalId`, `given_name`, `family_name`, `user_name`
- Status: `blocked`, `excluded`
- Personal: `gender`, `birth_date`, `initials`, `nick_name`, `honorific_prefix/suffix`
- Contact: `contact_business_*`, `contact_personal_*` (email, phone_fixed, phone_mobile, address_*)
- Custom: `custom_*` fields expanded from JSON

### Departments (departments.ps1)

```json
{
  "DisplayName": "Information Technology",
  "ExternalId": "dept-001",
  "code": "IT001",
  "manager_display_name": "Jane Smith",
  "manager_external_id": "manager-001",
  "parent_external_id": "company-root",
  "source": "HR-System-A"
}
```

**Department fields:**
- `DisplayName`, `ExternalId` - Standard HelloID fields
- `code` - Department code
- `manager_display_name`, `manager_external_id` - Department manager info
- `parent_external_id` - Parent department for hierarchy
- `source` - Source system identifier

## How It Works

```
PowerShell 5.1
      │
      ▼
Add-Type -Path "HelloID.PostgreSQL.dll"
      │
      ▼
[HelloID.PostgreSQL.Query]::Execute(connStr, query, params)
      │
      ▼
┌─────────────────────────────────────────┐
│ HelloID.PostgreSQL.dll                  │
│  (targets .NET Framework 4.7.2)         │
│                                         │
│  ┌─────────────────────────────────────┐│
│  │ Embedded Resources (13 DLLs)        ││
│  │  - Npgsql.dll (v8.0.8)              ││
│  │  - Microsoft.Extensions.Logging.*   ││
│  │  - Microsoft.Extensions.Dependency* ││
│  │  - Microsoft.Extensions.Options     ││
│  │  - Microsoft.Extensions.Primitives  ││
│  │  - System.Threading.Channels        ││
│  │  - System.Threading.Tasks.*.dll     ││
│  │  - System.Runtime.CompilerServices.*││
│  │  - System.Memory.dll                ││
│  │  - Microsoft.Bcl.AsyncInterfaces.dll││
│  │  - System.Text.Json.dll             ││
│  │  - System.Buffers.dll               ││
│  │  - System.Numerics.Vectors.dll      ││
│  └─────────────────────────────────────┘│
│                                         │
│  Internal AssemblyResolve handler       │
│  loads embedded dependencies            │
└─────────────────────────────────────────┘
      │
      ▼
   PostgreSQL Database
```

## Wrapper API Reference

The `HelloID.PostgreSQL.dll` wrapper exposes these methods:

### Read Operations

```powershell
# Execute SELECT query, returns DataTable
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, "SELECT * FROM persons")

# With parameters (prevents SQL injection)
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, @"
    SELECT * FROM persons WHERE source = @source
"@, @{ source = "HR-System-A" })

# Execute scalar query, returns single value
$count = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, "SELECT COUNT(*) FROM persons")

# Test connection
$ok = [HelloID.PostgreSQL.Query]::TestConnection($connStr)
```

### Write Operations

```powershell
# INSERT/UPDATE/DELETE - returns rows affected
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    INSERT INTO persons (external_id, display_name, source)
    VALUES (@id, @name, @source)
"@, @{ id = "p001"; name = "John Doe"; source = "Manual" })

# UPDATE with parameters
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    UPDATE persons SET display_name = @name WHERE external_id = @id
"@, @{ id = "p001"; name = "Jane Doe" })

# DELETE with parameters
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    DELETE FROM persons WHERE external_id = @id
"@, @{ id = "p001" })

# Bulk insert from DataTable (fast, uses COPY)
$rowsInserted = [HelloID.PostgreSQL.Query]::BulkInsert($connStr, "persons", $dataTable)

# Batch insert (slower, uses INSERT statements)
$rowsInserted = [HelloID.PostgreSQL.Query]::InsertBatch($connStr, "persons", $dataTable)

# Batch update (keyColumns identify rows to update)
$rowsUpdated = [HelloID.PostgreSQL.Query]::UpdateBatch($connStr, "persons", $dataTable, @("external_id"))

# Batch delete by keys
$rowsDeleted = [HelloID.PostgreSQL.Query]::DeleteBatch($connStr, "persons", "external_id", @("p001", "p002", "p003"))
```

### Transaction Support

```powershell
# Execute multiple operations in a transaction
[HelloID.PostgreSQL.Query]::ExecuteInTransaction($connStr, {
    param($tx)
    
    # Insert person
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO persons (external_id, display_name) VALUES (@id, @name)
"@, @{ id = "p001"; name = "John Doe" })
    
    # Insert contract
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO contracts (external_id, person_id) VALUES (@cid, @pid)
"@, @{ cid = "c001"; pid = "p001" })
})

# Transaction automatically commits on success, rolls back on error
```

### Convenience Methods

Simple APIs for common operations:

```powershell
# Update a single field
[HelloID.PostgreSQL.Query]::UpdateField($connStr, "persons", "display_name", "Jane Smith", "external_id", "p001")

# Update email in contacts (with additional WHERE clause)
[HelloID.PostgreSQL.Query]::UpdateField($connStr, "contacts", "email", "jane@company.com", "person_id", "p001", "AND type = 'Business'")

# Update multiple fields at once
[HelloID.PostgreSQL.Query]::UpdateFields($connStr, "persons", @{
    display_name = "Jane Smith"
    user_name = "jsmith"
}, "external_id", "p001")

# Upsert (insert or update) a single field
[HelloID.PostgreSQL.Query]::UpsertField($connStr, "persons", "display_name", "John Doe", "external_id", "p002")

# Upsert multiple fields
[HelloID.PostgreSQL.Query]::UpsertFields($connStr, "persons", @{
    display_name = "John Doe"
    user_name = "jdoe"
    source = "Manual"
}, "external_id", "p002")

# Insert a new record
[HelloID.PostgreSQL.Query]::Insert($connStr, "persons", @{
    external_id = "p003"
    display_name = "Bob Johnson"
    source = "Manual"
})

# Delete a record
[HelloID.PostgreSQL.Query]::Delete($connStr, "persons", "external_id", "p003")

# Check if record exists
$exists = [HelloID.PostgreSQL.Query]::Exists($connStr, "persons", "external_id", "p001")

# Get a single value
$email = [HelloID.PostgreSQL.Query]::GetValue([string], $connStr, "contacts", "email", "person_id", "p001", "AND type = 'Business'", "")

# Get display name for a person
$name = [HelloID.PostgreSQL.Query]::GetValue([string], $connStr, "persons", "display_name", "external_id", "p001")
```

## SSL Configuration

The connector uses these SSL settings:
- `SSL Mode=Prefer` - Uses SSL if available
- `Trust Server Certificate=true` - Accepts self-signed certificates

## Troubleshooting

### "Wrapper DLL not found"

Check the configured path is correct:
```powershell
Test-Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"
```

### "Unable to load one or more of the requested types"

This should not occur with the wrapper. If it does:
1. Ensure you're using the correct `HelloID.PostgreSQL.dll`
2. Rebuild the wrapper using `.\build.ps1`
3. Verify .NET Framework 4.7.2+ is installed on the HelloID server

### Build fails

Ensure you have:
- Windows OS (required for .NET Framework build)
- .NET SDK 8.0+ (for building the project)
- Internet access (to download Npgsql from NuGet)

### Connection issues

1. Verify network connectivity to PostgreSQL server
2. Check credentials are correct
3. Ensure firewall allows connections on the specified port
4. Confirm PostgreSQL accepts connections from HelloID server IP

## Security Notes

- Store database credentials securely in HelloID's encrypted configuration
- Use database users with minimal required permissions (READ ONLY recommended)
- The wrapper DLL does not store any credentials

## Files

| File | Purpose |
|------|---------|
| `persons.ps1` | Person import script |
| `departments.ps1` | Department import script |
| `configuration.json` | HelloID configuration schema |
| `README.md` | This file |
| `INSTALL.md` | Detailed installation guide |

## Getting Help

For more information on configuring HelloID PowerShell connectors, see the [HelloID documentation](https://docs.helloid.com/).
