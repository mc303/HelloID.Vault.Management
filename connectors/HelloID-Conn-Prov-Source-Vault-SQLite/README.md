# HelloID-Conn-Prov-Source-Vault-SQLite

HelloID source connector for Vault Management SQLite database using System.Data.SQLite.

## Version History

| Version | Description | Date |
|---------|-------------|------|
| 1.3.0 | Simplified DataRow conversion, flattened output structure | 2026-02-19 |
| 1.2.0 | Changed to flattened output structure, field exclusion | 2026-02-17 |
| 1.1.0 | Switched to System.Data.SQLite | 2026-02-17 |
| 1.0.0 | Initial release | 2026-02-16 |

## Overview

This connector imports persons, contracts, and departments from a Vault Management SQLite database into HelloID for identity provisioning. It uses `System.Data.SQLite` which is a native ADO.NET provider for SQLite that works directly with PowerShell 5.1 without requiring a wrapper DLL.

### Why System.Data.SQLite?

Unlike the Npgsql connector which requires a wrapper DLL for PowerShell 5.1 compatibility, System.Data.SQLite:

- Targets .NET Framework 4.0+ (native to PowerShell 5.1)
- Works as a single DLL without complex dependency chains
- Has native interop with SQLite's C library for optimal performance
- Is the official SQLite provider for .NET

### Features

- **Single DLL deployment** - Only `System.Data.SQLite.dll` needed
- **PowerShell 5.1 compatible** - Works on Windows Server 2016+
- **No server required** - SQLite is a file-based database
- **Flattened output** - All fields at root level with prefixed keys
- **Field exclusion** - Remove unwanted fields from output
- **Source filtering** - Import only data from specific source systems
- **Contract filtering** - Optionally include/exclude expired contracts
- **Contact flattening** - Business and personal contacts merged with prefixed keys

## Quick Start

1. **Download System.Data.SQLite**:
   - Visit: https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki
   - For PowerShell 5.1 on Windows, download the "Set for .NET Framework 4.6" (e.g., `sqlite-netFx46-binary-bundle-Win32-20xx-1.0.xxx.0.zip`)
   - Extract and copy `System.Data.SQLite.dll` to your connector folder

2. **Deploy to HelloID server**:
   ```
   Copy System.Data.SQLite.dll to C:\HelloID\SourceData\
   ```

3. **Configure connector** in HelloID:
   - Set Database Path to your SQLite database file
   - Set SQLite DLL Path to `C:\HelloID\SourceData\System.Data.SQLite.dll`

## Configuration

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| Database Path | Full path to the Vault SQLite database file | Yes | `C:\Data\vault.db` |
| SQLite DLL Path | Full path to System.Data.SQLite.dll | Yes | `C:\HelloID\SourceData\System.Data.SQLite.dll` |
| Source System Filter | Filter by source system | No | `HR-System-A` |
| Include Inactive Contracts | Include expired contracts | No | `false` |
| Fields to Exclude | Comma-separated fields to remove | No | `Source,custom_*` |
| Debug Mode | Enable verbose logging | No | `false` |

### Database Path Examples

**Standard location:**
| Setting | Value |
|---------|-------|
| Database Path | `C:\ProgramData\HelloID.Vault.Management\vault.db` |

**Network share:**
| Setting | Value |
|---------|-------|
| Database Path | `\\server\share\vault\vault.db` |

## SQLite Schema Specifics

This connector follows specific Vault Management SQLite schema conventions:

### Primary Keys
- Persons: `person_id` (TEXT, e.g., GUIDs)
- Departments: `external_id` + `source` (composite primary key)
- Contracts: `contract_id` (INTEGER, auto-increment)

### Foreign Keys
- Named with `_external_id` suffix (e.g., `department_external_id`)
- `department_external_id` → references department
- `manager_person_id` → references person (manager)

### Column Naming Conventions
| Vault SQLite | Not This |
|-------------|----------|
| `person_id` | ~~`person_external_id`~~ |
| `given_name` | ~~`first_name`~~ |
| `family_name` | ~~`last_name`~~ |
| `sequence` | ~~`sequence_number`~~ |
| `manager_person_id` | ~~`manager_person_external_id`~~ |
| `department_external_id` | ~~`department_id`~~ |

### JSON Fields
- `custom_fields` column stores JSON objects expanded to `custom_*` fields
- Contact information stored in separate `contacts` table with type-based prefixing

## Output Format

### Field Ordering Rules
1. **DisplayName** and **ExternalId** must appear first
2. Remaining fields sorted alphabetically
3. All fields always present, even when values are null

### Field Naming
- Nested data flattened to root level
- Prefixed with source table name (e.g., `location_name`, `cost_center_code`)
- Custom fields use underscore prefix (e.g., `custom_employee_id`)
- Contact fields prefixed with type (e.g., `contact_business_email`, `contact_personal_phone_mobile`)

### Persons (persons.ps1)

Returns person records with nested contracts array:

```json
{
  "DisplayName": "John Doe",
  "ExternalId": "person-001",
  "Contracts": [
    {
      "contract_end_date": "2025-12-31",
      "contract_external_id": "contract-001",
      "contract_fte": 1.0,
      "contract_hours_per_week": 40,
      "contract_sequence": 1,
      "contract_source": "HR-System-A",
      "contract_start_date": "2024-01-01",
      "contract_type_code": "PERM",
      "contract_type_description": "Permanent",
      "cost_center_code": "CC001",
      "cost_center_name": "IT Operations",
      "department_code": "IT001",
      "department_display_name": "Information Technology",
      "division_code": "TECH",
      "division_name": "Technology",
      "employer_code": "ACME",
      "employer_name": "ACME Corporation",
      "location_code": "AMS",
      "location_name": "Amsterdam",
      "manager_display_name": "Jane Smith",
      "manager_external_id": "manager-001",
      "organization_code": "HQ",
      "organization_name": "Headquarters",
      "team_code": "DEV",
      "team_name": "Development",
      "title_code": "DEV002",
      "title_name": "Senior Developer"
    }
  ],
  "blocked": false,
  "birth_date": "1990-01-15",
  "contact_business_email": "john@company.com",
  "contact_business_phone_fixed": "+31-20-1234567",
  "contact_business_phone_mobile": "+31-6-12345678",
  "contact_personal_email": null,
  "contact_personal_phone_fixed": null,
  "contact_personal_phone_mobile": null,
  "excluded": false,
  "family_name": "Doe",
  "gender": "M",
  "given_name": "John",
  "initials": "J",
  "nick_name": "Johnny",
  "primary_manager_person_id": "manager-001",
  "source": "HR-System-A",
  "user_name": "jdoe"
}
```

**Person fields include:**
- Core: `DisplayName`, `ExternalId`, `given_name`, `family_name`, `user_name`
- Status: `blocked`, `excluded`
- Personal: `gender`, `birth_date`, `initials`, `nick_name`, `honorific_prefix/suffix`
- Contact: `contact_business_*`, `contact_personal_*` (email, phone_fixed, phone_mobile, address_*)
- Manager: `primary_manager_person_id`
- Custom: `custom_*` fields expanded from JSON
- Contracts: Nested array with all contract and lookup data

### Departments (departments.ps1)

```json
{
  "DisplayName": "Information Technology",
  "ExternalId": "dept-001",
  "ManagerExternalId": "manager-001",
  "code": "IT001",
  "manager_display_name": "Jane Smith",
  "manager_person_id": "manager-001",
  "parent_external_id": "company-root",
  "source": "HR-System-A"
}
```

**Department fields:**
- `DisplayName`, `ExternalId` - Standard HelloID fields
- `ManagerExternalId`, `manager_display_name`, `manager_person_id` - Department manager info
- `parent_external_id` - Parent department for hierarchy
- `code` - Department code
- `source` - Source system identifier

## Database Schema

The connector expects the following SQLite tables:

### Core Tables

| Table | Description | Key Columns |
|-------|-------------|-------------|
| `persons` | Person records | `person_id` (PK), `external_id`, `display_name` |
| `contracts` | Employment contracts | `contract_id` (PK), `person_id` (FK), `department_external_id` |
| `departments` | Department hierarchy | `external_id` + `source` (composite PK) |
| `contacts` | Contact information | `contact_id` (PK), `person_id` (FK), `type` |

### Lookup Tables

| Table | Description |
|-------|-------------|
| `titles` | Job titles |
| `locations` | Work locations |
| `cost_centers` | Cost centers |
| `cost_bearers` | Cost bearers |
| `employers` | Employers/legal entities |
| `teams` | Teams |
| `divisions` | Divisions |
| `organizations` | Organizations |
| `source_system` | Source system tracking |

### Foreign Key Relationships

```
contracts.person_id → persons.person_id
contracts.manager_person_external_id → persons.person_id
contracts.department_external_id → departments.external_id
departments.manager_person_id → persons.person_id
contacts.person_id → persons.person_id
```

## How It Works

```
PowerShell 5.1
      │
      ▼
Add-Type -Path "System.Data.SQLite.dll"
      │
      ▼
System.Data.SQLite.SQLiteConnection
      │
      ▼
┌─────────────────────────────────────────────────────┐
│ System.Data.SQLite.dll                              │
│  (targets .NET Framework 4.0+)                      │
│                                                     │
│  - Native ADO.NET provider for SQLite               │
│  - Direct interop with SQLite C library             │
│  - Single DLL, no complex dependencies              │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │ SQLite Native Library                         │  │
│  │  - Embedded in DLL (static linking)           │  │
│  │  - No separate sqlite3.dll needed             │  │
│  │  - Platform-specific (x86/x64)                │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
      │
      ▼
   SQLite Database File (vault.db)
```

## Troubleshooting

### "Failed to load System.Data.SQLite assembly"

**Cause:** The DLL path is incorrect or the DLL is missing.

**Solution:**
```powershell
# Verify the path
Test-Path "C:\HelloID\SourceData\System.Data.SQLite.dll"
# Should return: True

# Check file exists and has content
Get-Item "C:\HelloID\SourceData\System.Data.SQLite.dll" | Select-Object Name, Length
```

### "Unable to load DLL 'SQLite.Interop.dll'"

**Cause:** Architecture mismatch (x86 vs x64).

**Solution:**
1. Ensure you downloaded the correct version for your system:
   - For 64-bit Windows: Download "Win32" or "x64" version
   - For 32-bit Windows: Download "Win32" or "x86" version
2. Verify PowerShell is running in correct bitness:
   ```powershell
   # Check if running 64-bit PowerShell
   [Environment]::Is64BitProcess
   ```

### "Database is locked"

**Cause:** Another process has the database open with exclusive lock.

**Solution:**
1. Close any other applications using the database
2. The connector uses WAL mode for better concurrency - ensure the database was created with:
   ```sql
   PRAGMA journal_mode = WAL;
   ```
3. Check for `.wal` and `.shm` files in the database directory

### "Database disk image is malformed"

**Cause:** Database file corruption.

**Solution:**
```powershell
# Check database integrity
sqlite3 vault.db "PRAGMA integrity_check;"

# If corruption found, restore from backup or export/import:
sqlite3 vault.db ".dump" > backup.sql
sqlite3 vault_fixed.db < backup.sql
```

### "no such table: persons"

**Cause:** Database path incorrect or tables not created.

**Solution:**
1. Verify the database path is correct
2. Check the database contains required tables:
   ```sql
   SELECT name FROM sqlite_master WHERE type='table';
   ```
3. Run the schema creation script if tables are missing:
   ```powershell
   sqlite3 vault.db < sqlite_schema.sql
   ```

### Query returns no data

**Cause:** Source filter or data filtering.

**Solution:**
1. Clear the "Source System Filter" to import all data
2. Enable "Debug Mode" to see SQL queries being executed
3. Test query directly:
   ```sql
   SELECT COUNT(*) FROM persons;
   SELECT DISTINCT source FROM persons;
   ```

### PowerShell session crashes or hangs

**Cause:** Memory issues or DLL conflicts.

**Solution:**
1. Close and reopen PowerShell to get a fresh session
2. Verify .NET Framework 4.6+ is installed:
   ```powershell
   Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' | 
     Get-ItemProperty -Name Release | 
     Select-Object Release
   # Release >= 393295 means 4.6+
   ```
3. Try the test script to diagnose:
   ```powershell
   .\test-sqlite.ps1 -DllPath "C:\path\to\System.Data.SQLite.dll"
   ```

### File not found: vault.db

**Cause:** Database path with spaces or special characters.

**Solution:**
1. Use full path without trailing spaces
2. Avoid paths with special characters
3. Use forward slashes or escaped backslashes:
   ```
   C:/Data/vault.db
   C:\\Data\\vault.db
   ```

## Security Notes

- SQLite database files should be on a secure, access-controlled location
- Use Windows file permissions to restrict database access
- The connector reads data only - no write operations
- Database path and DLL path are stored in HelloID's encrypted configuration
- Consider encrypting sensitive SQLite databases with SQLCipher

## Performance Considerations

- SQLite is file-based - place database on fast storage (SSD recommended)
- The connector uses parameterized queries for SQL injection prevention
- WAL mode enables better read concurrency
- Large datasets may benefit from batch processing
- Enable Debug Mode to identify slow queries

## Files

| File | Purpose |
|------|---------|
| `persons.ps1` | Person import script |
| `departments.ps1` | Department import script |
| `configuration.json` | HelloID configuration schema |
| `test-sqlite.ps1` | Connection test and diagnostic script |
| `README.md` | This file |

## Getting Help

For more information on configuring HelloID PowerShell connectors, see the [HelloID documentation](https://docs.helloid.com/).

For issues specific to this connector:
1. Enable Debug Mode and review the logs
2. Run the test script: `.\test-sqlite.ps1`
3. Check the Troubleshooting section above
