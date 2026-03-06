# Changelog - HelloID-Conn-Prov-Source-Vault-Turso

All notable changes to this connector will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).
    Dates should be in YYYY-MM-DD format.

## [1.0.7] - 2026-03-06

### Fixed
- Fixed null handling: Turso returns `{"type":"null"}` without value property
- ConvertFrom-TursoValue now checks for type="null" BEFORE checking value property
- Null values now properly returned as `$null` instead of `"@{type=null}"`

## [1.0.6] - 2026-03-04

### Fixed
- Type conversion: Turso sends all values as strings in JSON
- ConvertFrom-TursoValue now converts based on type field (integer→[long], float→[double])
- Numbers and dates now exported with correct types instead of all text

## [1.0.5] - 2026-03-04

### Fixed
- Added ConvertFrom-TursoValue helper function to extract values from Turso typed format
- Fixed "@{type=null}" appearing instead of null values in contacts and contracts
- Updated ConvertTo-FlatHashtable to properly convert all Turso typed values
- Fixed empty contracts issue by ensuring all contract field values are converted

## [1.0.4] - 2026-03-04

### Fixed
- Output JSON string instead of PSCustomObject (matches SQLite connector pattern)
- Fixed "unsupported type: System.Management.Automation.PSCustomObject" error

## [1.0.3] - 2026-03-04

### Fixed
- Replaced Test-ShouldExcludeField function with simple loop pattern (matches SQLite connector)
- Fixed parameter binding error caused by scoping issue in HelloID execution context

## [1.0.2] - 2026-03-04

### Fixed
- Fixed Turso HTTP API response parsing using PSObject.Properties.Match()
- Explicit array initialization for $excludedFields to avoid null binding errors
- Handle Turso's typed response format: @{type="..."; value="..."}

## [1.0.1] - 2026-03-04

### Fixed
- Removed unsupported [decimal] and [float] type checks for PowerShell 5.1 compatibility
- Added IsNullOrWhiteSpace check for fieldsToExclude configuration
- Skip invalid person records (null person_id or empty external_id)
- Use string keys for person_id hashtable lookups (contracts/contacts)

## [1.0.0] - 2026-03-04

### Added
- Initial release of Turso source connector
- Direct Turso HTTP API integration (libsql:// and https:// URLs)
- JWT token authentication
- Source filtering support
- Field exclusion support
- Person import with contracts and contacts
- Department import with manager info
