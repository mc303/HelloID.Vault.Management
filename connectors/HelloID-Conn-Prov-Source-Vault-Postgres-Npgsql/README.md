# HelloID-Conn-Prov-Source-Vault-Postgres-Npgsql

HelloID source connector for Vault Management PostgreSQL database using a .NET Framework wrapper DLL.

**📖 [Installation Guide](INSTALL.md)** - Step-by-step build and deploy instructions

## Version History

| Version | Description | Date |
|---------|-------------|------|
| 0.7.0 | Added Npgsql 4.0.12 build option for constrained environments | 2026-02-19 |
| 0.6.0 | Uses HelloID.PostgreSQL wrapper DLL (single-file deployment) | 2026-02-18 |
| 0.5.0 | Assembly resolution for PowerShell 5.1 | 2026-02-18 |
| 0.1.0 | Initial release | 2026-02-16 |

## Overview

This connector imports persons, contracts, and departments from a Vault Management PostgreSQL database into HelloID. It uses a custom .NET Framework 4.7.2 wrapper DLL that embeds Npgsql internally, eliminating PowerShell 5.1 assembly loading issues.

### Why the Wrapper DLL?

PowerShell 5.1 (default on Windows Server) cannot properly load .NET Standard assemblies like Npgsql. The wrapper DLL:

- Targets .NET Framework 4.7.2 (native to PowerShell 5.1)
- Embeds Npgsql and all dependencies internally as embedded resources
- Extracts dependencies at runtime to a temp folder
- Handles assembly resolution automatically via AssemblyResolve event
- Provides a simple, clean API via `[HelloID.PostgreSQL.Query]`

### Npgsql Version Options

Two Npgsql versions are supported to accommodate different environments:

#### Npgsql 8.0.8 (Default)

Best for most environments. Includes 14 embedded DLLs:
- Npgsql.dll
- Microsoft.Extensions.Logging.Abstractions.dll
- Microsoft.Extensions.DependencyInjection.Abstractions.dll
- Microsoft.Extensions.Options.dll
- Microsoft.Extensions.Primitives.dll
- System.Threading.Tasks.Extensions.dll
- System.Runtime.CompilerServices.Unsafe.dll
- System.Memory.dll
- Microsoft.Bcl.AsyncInterfaces.dll
- System.Text.Json.dll
- System.Buffers.dll
- System.Numerics.Vectors.dll
- System.Threading.Channels.dll
- System.Diagnostics.DiagnosticSource.dll

**Build command:** `.\build.ps1` or `build.bat`

#### Npgsql 4.0.12 (Alternative)

Best for environments with stricter assembly loading constraints (e.g., servers with GAC conflicts or locked-down assemblies). Includes 6 embedded DLLs:
- Npgsql.dll
- System.Threading.Tasks.Extensions.dll
- System.Runtime.CompilerServices.Unsafe.dll
- System.Memory.dll
- System.ValueTuple.dll
- System.Diagnostics.DiagnosticSource.dll

**Build command:** `.\build-4.0.12.ps1` or `build-4.0.12.bat`

> **When to use 4.0.12:** If you encounter assembly loading errors with 8.0.8 related to Microsoft.Extensions.* or System.* assemblies already being loaded in the PowerShell session.

### Features

- **Single DLL deployment** - Only `HelloID.PostgreSQL.dll` needed
- **PowerShell 5.1 compatible** - Works on Windows Server 2016+
- **Two Npgsql versions** - Choose based on environment constraints
- **Flattened output** - All fields at root level with prefixed keys
- **Field exclusion** - Remove unwanted fields from output
- **Source filtering** - Import only data from specific source systems
- **Contract filtering** - Optionally include/exclude expired contracts
- **Full CRUD API** - Read, write, bulk operations, and transactions

## Quick Start

1. **Build the wrapper DLL** (Windows required):
   ```powershell
   # Option 1: Npgsql 8.0.8 (recommended)
   cd src\HelloID.PostgreSQL
   .\build.ps1
   
   # Option 2: Npgsql 4.0.12 (alternative)
   .\build-4.0.12.ps1
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

## PostgreSQL Schema Specifics

This connector follows specific Vault Management PostgreSQL schema conventions:

### Primary Keys
- Persons: `person_id` (integer, auto-increment)
- Departments: `department_id` (integer, auto-increment)
- Contracts: `contract_id` (integer, auto-increment)

### Foreign Keys
- Named with `_external_id` suffix (e.g., `department_external_id`)
- `department_external_id` → references department
- `manager_person_id` → references person (manager)

### Column Naming Conventions
| Vault PostgreSQL | Not This |
|-----------------|----------|
| `person_id` | ~~`person_external_id`~~ |
| `given_name` | ~~`first_name`~~ |
| `family_name` | ~~`last_name`~~ |
| `sequence` | ~~`sequence_number`~~ |
| `manager_person_id` | ~~`manager_person_external_id`~~ |
| `department_external_id` | ~~`department_id`~~ |

### JSON Fields
- `custom_data` column stores JSON objects expanded to `custom_*` fields
- `contact_info` column stores JSON with nested contact details

## Output Format

### Field Ordering Rules
1. **DisplayName** and **ExternalId** must appear first
2. Remaining fields sorted alphabetically
3. All fields always present, even when values are null

### Field Naming
- Nested data flattened to root level
- Prefixed with source table name (e.g., `location_name`, `cost_center_code`)
- Custom fields use underscore prefix (e.g., `custom_employee_id`)

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
┌─────────────────────────────────────────────────────┐
│ HelloID.PostgreSQL.dll                              │
│  (targets .NET Framework 4.7.2)                     │
│                                                     │
│  1. Extracts embedded DLLs to temp folder           │
│  2. Registers AssemblyResolve event handler         │
│  3. Loads Npgsql and dependencies on demand         │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │ Embedded Resources (Npgsql 8.0.8 - 14 DLLs)   │  │
│  │  - Npgsql.dll                                 │  │
│  │  - Microsoft.Extensions.Logging.Abstractions  │  │
│  │  - Microsoft.Extensions.DependencyInjection.* │  │
│  │  - Microsoft.Extensions.Options/Primitives    │  │
│  │  - System.Threading.Channels/Tasks.Extensions │  │
│  │  - System.Runtime.CompilerServices.Unsafe     │  │
│  │  - System.Memory/Buffers/Numerics.Vectors     │  │
│  │  - Microsoft.Bcl.AsyncInterfaces              │  │
│  │  - System.Text.Json                           │  │
│  │  - System.Diagnostics.DiagnosticSource        │  │
│  │                                               │  │
│  │                    - OR -                     │  │
│  │                                               │  │
│  │ Embedded Resources (Npgsql 4.0.12 - 6 DLLs)   │  │
│  │  - Npgsql.dll                                 │  │
│  │  - System.Threading.Tasks.Extensions          │  │
│  │  - System.Runtime.CompilerServices.Unsafe     │  │
│  │  - System.Memory                              │  │
│  │  - System.ValueTuple                          │  │
│  │  - System.Diagnostics.DiagnosticSource        │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
      │
      ▼
   PostgreSQL Database
```

## Wrapper API Reference

The `HelloID.PostgreSQL.dll` wrapper exposes these static methods on the `HelloID.PostgreSQL.Query` class:

### Read Operations

#### Execute() - SELECT queries

Returns a DataTable with query results. Supports parameterized queries for SQL injection prevention.

```powershell
# Simple SELECT query
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, "SELECT * FROM persons")

# With parameters (prevents SQL injection)
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, @"
    SELECT * FROM persons WHERE source = @source AND blocked = @blocked
"@, @{ 
    source = "HR-System-A"
    blocked = $false 
})

# Process results
foreach ($row in $table.Rows) {
    Write-Host "Person: $($row['display_name'])"
}
```

#### ExecuteScalar() - Single value queries

Returns a single value (first column of first row). Use for COUNT, SUM, or single-value lookups.

```powershell
# Count records
$count = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, "SELECT COUNT(*) FROM persons")

# Get max value
$maxId = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, "SELECT MAX(person_id) FROM persons")

# With parameters
$name = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, @"
    SELECT display_name FROM persons WHERE person_id = @id
"@, @{ id = 123 })
```

#### TestConnection() - Connection validation

Tests if a connection can be established. Returns `true` or `false`.

```powershell
$ok = [HelloID.PostgreSQL.Query]::TestConnection($connStr)
if ($ok) {
    Write-Host "Connection successful"
} else {
    Write-Host "Connection failed"
}
```

#### Exists() - Check record existence

Checks if a record exists matching the specified key value.

```powershell
# Check if person exists
$exists = [HelloID.PostgreSQL.Query]::Exists($connStr, "persons", "person_id", 123)

# Check by external_id
$exists = [HelloID.PostgreSQL.Query]::Exists($connStr, "persons", "external_id", "person-001")

# With additional WHERE clause
$exists = [HelloID.PostgreSQL.Query]::Exists($connStr, "persons", "external_id", "person-001", "AND blocked = 0")
```

#### GetValue<T>() - Get typed single field

Gets a single field value with type conversion. Returns default(T) if not found.

```powershell
# Get string value
$email = [HelloID.PostgreSQL.Query]::GetValue([string], $connStr, "persons", "user_name", "person_id", 123)

# Get integer value
$id = [HelloID.PostgreSQL.Query]::GetValue([int], $connStr, "persons", "person_id", "external_id", "person-001")

# With additional WHERE clause (for complex lookups)
$email = [HelloID.PostgreSQL.Query]::GetValue([string], $connStr, "contacts", "email", "person_id", 123, "AND type = 'Business'", "")

# With fallback value (5th parameter)
$name = [HelloID.PostgreSQL.Query]::GetValue([string], $connStr, "persons", "display_name", "person_id", 999, "", "Unknown Person")
```

---

### Write Operations

#### ExecuteNonQuery() - INSERT/UPDATE/DELETE

Executes a non-query SQL statement. Returns the number of rows affected.

```powershell
# INSERT
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    INSERT INTO persons (external_id, display_name, given_name, family_name, source)
    VALUES (@id, @displayName, @givenName, @familyName, @source)
"@, @{ 
    id = "p001"
    displayName = "John Doe"
    givenName = "John"
    familyName = "Doe"
    source = "Manual"
})

# UPDATE
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    UPDATE persons 
    SET display_name = @name, blocked = @blocked 
    WHERE external_id = @id
"@, @{ 
    id = "p001"
    name = "Jane Doe"
    blocked = $false
})

# DELETE
$rows = [HelloID.PostgreSQL.Query]::ExecuteNonQuery($connStr, @"
    DELETE FROM persons WHERE external_id = @id
"@, @{ id = "p001" })

Write-Host "$rows row(s) affected"
```

#### Insert() - Insert from dictionary

Inserts a new record from a hashtable/dictionary.

```powershell
[HelloID.PostgreSQL.Query]::Insert($connStr, "persons", @{
    external_id = "p003"
    display_name = "Bob Johnson"
    given_name = "Bob"
    family_name = "Johnson"
    source = "Manual"
    blocked = $false
})
```

#### BulkInsert() - Fast bulk insert using COPY

Fast bulk insert using PostgreSQL COPY protocol. Best for large datasets.

```powershell
# Create DataTable with data
$dt = New-Object System.Data.DataTable
[void]$dt.Columns.Add("external_id", [string])
[void]$dt.Columns.Add("display_name", [string])
[void]$dt.Columns.Add("source", [string])

# Add rows
$row = $dt.NewRow()
$row["external_id"] = "p001"
$row["display_name"] = "John Doe"
$row["source"] = "Manual"
$dt.Rows.Add($row)

# Bulk insert
$rowsInserted = [HelloID.PostgreSQL.Query]::BulkInsert($connStr, "persons", $dt)
Write-Host "Inserted $rowsInserted rows"
```

#### InsertBatch() - Batch insert using INSERT statements

Batch insert using parameterized INSERT statements. Slower than BulkInsert but more compatible.

```powershell
$rowsInserted = [HelloID.PostgreSQL.Query]::InsertBatch($connStr, "persons", $dataTable)
```

#### UpdateBatch() - Batch update by key columns

Updates multiple records identified by key columns.

```powershell
# Update records where external_id matches
$rowsUpdated = [HelloID.PostgreSQL.Query]::UpdateBatch($connStr, "persons", $dataTable, @("external_id"))

# Multiple key columns
$rowsUpdated = [HelloID.PostgreSQL.Query]::UpdateBatch($connStr, "contracts", $dataTable, @("person_id", "sequence"))
```

#### DeleteBatch() - Batch delete by key values

Deletes records matching a list of key values.

```powershell
# Delete by external_id list
$rowsDeleted = [HelloID.PostgreSQL.Query]::DeleteBatch($connStr, "persons", "external_id", @("p001", "p002", "p003"))

# Delete by integer keys
$rowsDeleted = [HelloID.PostgreSQL.Query]::DeleteBatch($connStr, "contracts", "contract_id", @(1, 2, 3, 4, 5))
```

---

### Convenience Methods

#### UpdateField() - Update single field

Updates a single field in a record.

```powershell
# Basic update
[HelloID.PostgreSQL.Query]::UpdateField($connStr, "persons", "display_name", "Jane Smith", "external_id", "p001")

# With additional WHERE clause
[HelloID.PostgreSQL.Query]::UpdateField($connStr, "contacts", "email", "jane@company.com", "person_id", 123, "AND type = 'Business'")
```

#### UpdateFields() - Update multiple fields

Updates multiple fields in a single operation.

```powershell
[HelloID.PostgreSQL.Query]::UpdateFields($connStr, "persons", @{
    display_name = "Jane Smith"
    user_name = "jsmith"
    blocked = $false
}, "external_id", "p001")
```

#### UpsertField() - Insert or update single field

Inserts a new record or updates the field if the record exists.

```powershell
[HelloID.PostgreSQL.Query]::UpsertField($connStr, "persons", "display_name", "John Doe", "external_id", "p002")
```

#### UpsertFields() - Insert or update multiple fields

Inserts a new record or updates multiple fields if the record exists.

```powershell
[HelloID.PostgreSQL.Query]::UpsertFields($connStr, "persons", @{
    display_name = "John Doe"
    user_name = "jdoe"
    given_name = "John"
    family_name = "Doe"
    source = "Manual"
}, "external_id", "p002")
```

#### Delete() - Delete by key

Deletes a record by its key value.

```powershell
[HelloID.PostgreSQL.Query]::Delete($connStr, "persons", "external_id", "p003")
```

---

### Transaction Support

All operations within a transaction are atomic - either all succeed or all fail.

#### ExecuteInTransaction() - Atomic multi-operation

Executes multiple operations in a single transaction. Automatically commits on success, rolls back on error.

```powershell
[HelloID.PostgreSQL.Query]::ExecuteInTransaction($connStr, {
    param($tx)
    
    # Insert person
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO persons (external_id, display_name, given_name, family_name)
        VALUES (@id, @displayName, @givenName, @familyName)
"@, @{ 
        id = "p001"
        displayName = "John Doe"
        givenName = "John"
        familyName = "Doe"
    })
    
    # Insert contract
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO contracts (external_id, person_external_id, start_date)
        VALUES (@cid, @pid, @startDate)
"@, @{ 
        cid = "c001"
        pid = "p001"
        startDate = "2024-01-01"
    })
    
    # Update department count
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        UPDATE departments SET person_count = person_count + 1 
        WHERE external_id = @deptId
"@, @{ deptId = "dept-001" })
})
```

#### ExecuteNonQueryInTransaction() - Write in transaction

Execute INSERT/UPDATE/DELETE within a transaction.

```powershell
[HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, $sql, $parameters)
```

#### ExecuteScalarInTransaction() - Read in transaction

Execute a scalar query within a transaction (useful for reading generated IDs).

```powershell
[HelloID.PostgreSQL.Query]::ExecuteInTransaction($connStr, {
    param($tx)
    
    # Insert and get new ID
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO persons (external_id, display_name) 
        VALUES (@id, @name)
"@, @{ id = "p001"; name = "John Doe" })
    
    # Read the generated ID
    $newId = [HelloID.PostgreSQL.Query]::ExecuteScalarInTransaction($tx, @"
        SELECT person_id FROM persons WHERE external_id = @id
"@, @{ id = "p001" })
    
    # Use the ID for related insert
    [HelloID.PostgreSQL.Query]::ExecuteNonQueryInTransaction($tx, @"
        INSERT INTO contracts (person_id, start_date) 
        VALUES (@personId, @startDate)
"@, @{ personId = $newId; startDate = "2024-01-01" })
})
```

---

## SSL Configuration

The connector uses these SSL settings:
- `SSL Mode=Prefer` - Uses SSL if available
- `Trust Server Certificate=true` - Accepts self-signed certificates

For stricter SSL requirements, modify the connection string in your scripts.

---

## Troubleshooting

### "Wrapper DLL not found"

**Cause:** The configured path to HelloID.PostgreSQL.dll is incorrect.

**Solution:**
```powershell
# Verify the path
Test-Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"
# Should return: True

# Check file exists and has content
Get-Item "C:\HelloID\SourceData\HelloID.PostgreSQL.dll" | Select-Object Name, Length
```

### "Unable to load one or more of the requested types"

**Cause:** Assembly loading conflict in PowerShell 5.1.

**Solution:**
1. First, try rebuilding with Npgsql 4.0.12:
   ```powershell
   cd src\HelloID.PostgreSQL
   .\build-4.0.12.ps1
   # Redeploy the DLL
   ```
2. Verify .NET Framework 4.7.2+ is installed:
   ```powershell
   Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' | 
     Get-ItemProperty -Name Release | 
     Select-Object Release
   # Release >= 461808 means 4.7.2+
   ```

### "Could not load file or assembly 'Microsoft.Extensions.*'"

**Cause:** GAC or PowerShell session has conflicting assembly versions.

**Solution:** Use the Npgsql 4.0.12 build which has fewer dependencies:
```powershell
.\build-4.0.12.ps1
```

### "Connection refused" or "Connection timeout"

**Cause:** Network connectivity issues.

**Solutions:**
1. Verify PostgreSQL is running:
   ```powershell
   Test-NetConnection -ComputerName "db-server.example.com" -Port 5432
   ```
2. Check firewall allows outbound connections on the port
3. Verify PostgreSQL `pg_hba.conf` allows connections from HelloID server IP
4. For Aiven: Use the correct port from the Aiven console (not 5432)

### "Authentication failed"

**Cause:** Invalid credentials.

**Solutions:**
1. Verify username and password are correct
2. Check for special characters that need escaping in connection string
3. For Aiven: Download the CA certificate if using SSL verification

### Query returns no data

**Cause:** Source filter or data filtering.

**Solutions:**
1. Clear the "Source System Filter" to import all data
2. Enable "Debug Mode" to see SQL queries being executed
3. Test query directly in pgAdmin or psql:
   ```sql
   SELECT COUNT(*) FROM persons;
   SELECT DISTINCT source FROM persons;
   ```

### PowerShell session crashes or hangs

**Cause:** Memory issues or infinite wait.

**Solutions:**
1. Close and reopen PowerShell to get a fresh session
2. Use smaller batch sizes for bulk operations
3. Add timeouts to long-running queries:
   ```powershell
   $connStr = "Host=...;Timeout=60;Command Timeout=300"
   ```

### Temp folder issues

**Cause:** Embedded DLLs extracted to temp folder inaccessible.

**Solution:** The wrapper extracts DLLs to `%TEMP%\HelloID.PostgreSQL\`. Check:
```powershell
dir $env:TEMP\HelloID.PostgreSQL\
```

### Build fails with "nuget.exe not found"

**Cause:** NuGet download failed.

**Solution:** Download manually:
```powershell
Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "nuget.exe"
```

---

## Security Notes

- Store database credentials securely in HelloID's encrypted configuration
- Use database users with minimal required permissions (READ ONLY recommended for source connectors)
- The wrapper DLL does not store any credentials
- Connection strings are constructed at runtime from HelloID configuration
- SSL is preferred for all connections (configured automatically)

---

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

For issues specific to this connector:
1. Enable Debug Mode and review the logs
2. Test the connection manually using the Verify steps in INSTALL.md
3. Check the Troubleshooting section above
