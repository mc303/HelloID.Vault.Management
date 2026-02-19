# HelloID-Conn-Prov-Source-Vault-Supabase

HelloID source connector for Vault Management Supabase PostgreSQL database using a .NET Framework wrapper DLL.

## Version History

| Version | Description | Date |
|---------|-------------|------|
| 2.0.0 | Uses HelloID.PostgreSQL wrapper DLL for direct database access | 2026-02-19 |
| 1.0.0 | Initial release (PostgREST API) | 2026-02-16 |

## Overview

This connector imports persons, contracts, and departments from a Vault Management Supabase PostgreSQL database into HelloID. It uses the same custom .NET Framework 4.7.2 wrapper DLL (`HelloID.PostgreSQL.dll`) as the Npgsql connector, which embeds Npgsql internally to eliminate PowerShell 5.1 assembly loading issues.

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
- **Supabase pooler support** - Connection pooling via port 6543
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
   - Enter Supabase connection details (see Connection Examples below)

## Configuration

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| Wrapper DLL Path | Full path to HelloID.PostgreSQL.dll | Yes | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| Host | Supabase database host | Yes | `db.xxxxx.supabase.co` |
| Port | Supabase port | Yes | `5432` (direct) or `6543` (pooler) |
| Database | Database name | Yes | `postgres` |
| Username | Database username | Yes | `postgres` |
| Password | Database password | Yes | - |
| Source System Filter | Filter by source system | No | `HR-System-A` |
| Include Inactive Contracts | Include expired contracts | No | `false` |
| Fields to Exclude | Comma-separated fields to remove | No | `Source,custom_*` |
| Debug Mode | Enable verbose logging | No | `false` |

## Supabase Connection Details

### Obtaining Database Credentials

1. Log in to your Supabase dashboard
2. Navigate to **Project Settings** > **Database**
3. Find the **Connection string** section
4. Copy the connection details:
   - **Host**: The database host (e.g., `db.xxxxx.supabase.co`)
   - **Port**: Use `5432` for direct connection or `6543` for connection pooler
   - **Database**: `postgres` (default)
   - **Username**: `postgres`
   - **Password**: Your database password (set during project creation)

### Connection Examples

**Direct Connection (Port 5432):**
| Setting | Value |
|---------|-------|
| Host | `db.xxxxxx.supabase.co` |
| Port | `5432` |
| Database | `postgres` |
| Username | `postgres` |

**Connection Pooler (Port 6543) - Recommended for Production:**
| Setting | Value |
|---------|-------|
| Host | `db.xxxxxx.supabase.co` |
| Port | `6543` |
| Database | `postgres` |
| Username | `postgres.xxxxxx` (note: includes project ref) |

> **Pooler Recommendation:** Use port 6543 (Supavisor pooler) for production workloads to avoid connection limits. The pooler shares connections across multiple clients.

### Connection Pooler (Supavisor)

Supabase provides a built-in connection pooler called Supavisor:

- **Port 6543**: Session mode (recommended for most use cases)
- **Port 6542**: Transaction mode (for serverless/short-lived connections)

**Benefits of using the pooler:**
- Avoids "too many connections" errors
- Better resource utilization
- Supports higher concurrent connection counts
- Automatic connection management

**Pooler username format:**
- Direct connection: `postgres`
- Pooler connection: `postgres.YOUR_PROJECT_REF` (find in Supabase dashboard)

## PostgreSQL Schema Specifics (Supabase)

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
   Supabase PostgreSQL
   (db.xxxxx.supabase.co:5432 or :6543)
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

# Add rows
$row = $dt.NewRow()
$row["external_id"] = "p001"
$row["display_name"] = "John Doe"
$dt.Rows.Add($row)

# Bulk insert
$rowsInserted = [HelloID.PostgreSQL.Query]::BulkInsert($connStr, "persons", $dt)
```

---

## SSL Configuration

Supabase requires SSL connections. The connector uses these SSL settings:
- `SSL Mode=Require` - SSL is required for Supabase
- `Trust Server Certificate=true` - Accepts Supabase certificates

The connection string is automatically configured for Supabase SSL requirements.

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
1. Verify Supabase is accessible:
   ```powershell
   Test-NetConnection -ComputerName "db.xxxxxx.supabase.co" -Port 5432
   ```
2. Check firewall allows outbound connections on port 5432 or 6543
3. Ensure your IP is allowed in Supabase dashboard (Settings > Database > Connection Pooling)

### "too many connections" error

**Cause:** Exceeded Supabase connection limits.

**Solution:** Use the connection pooler (port 6543) instead of direct connection (port 5432):
- Change port from `5432` to `6543`
- Update username to `postgres.YOUR_PROJECT_REF` format
- The pooler manages connection sharing automatically

### "password authentication failed"

**Cause:** Invalid credentials.

**Solutions:**
1. Verify the database password in Supabase dashboard
2. For pooler connections, ensure username includes project ref: `postgres.xxxxxx`
3. Check for special characters that need escaping in connection string
4. Reset database password in Supabase dashboard if needed

### Query returns no data

**Cause:** Source filter or data filtering.

**Solutions:**
1. Clear the "Source System Filter" to import all data
2. Enable "Debug Mode" to see SQL queries being executed
3. Test query directly in Supabase SQL Editor:
   ```sql
   SELECT COUNT(*) FROM persons;
   SELECT DISTINCT source FROM persons;
   ```

### Row Level Security (RLS) blocking queries

**Cause:** Supabase RLS policies restricting access.

**Solution:** The connector uses database credentials (not API keys), which bypass RLS by default when using the `postgres` role. If RLS is explicitly enabled:
1. Check RLS policies in Supabase dashboard > Authentication > Policies
2. Ensure `postgres` role has SELECT permissions on required tables
3. Or disable RLS for the schema used by this connector

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
- Use the connection pooler (port 6543) for production to avoid connection exhaustion
- The `postgres` role bypasses Row Level Security - use appropriate database roles if needed
- The wrapper DLL does not store any credentials
- Connection strings are constructed at runtime from HelloID configuration
- SSL is required for all Supabase connections (configured automatically)

---

## Files

| File | Purpose |
|------|---------|
| `persons.ps1` | Person import script |
| `departments.ps1` | Department import script |
| `configuration.json` | HelloID configuration schema |
| `README.md` | This file |

## Related Resources

- **Wrapper DLL source:** `src/HelloID.PostgreSQL/` (build scripts and source code)
- **Npgsql connector:** `connectors/HelloID-Conn-Prov-Source-Vault-Postgres-Npgsql/` (uses same DLL)
- **Supabase docs:** https://supabase.com/docs/guides/database/connecting-to-postgres

## Getting Help

For more information on configuring HelloID PowerShell connectors, see the [HelloID documentation](https://docs.helloid.com/).

For issues specific to this connector:
1. Enable Debug Mode and review the logs
2. Test the connection using a SQL client (Supabase SQL Editor or pgAdmin)
3. Check the Troubleshooting section above
