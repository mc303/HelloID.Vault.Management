# Changelog - HelloID-Conn-Prov-Target-Vault-Turso

All notable changes to this connector will be documented in this file.

## [1.0.5] - 2026-03-06

### Fixed
- Fixed parameter ordering in contact UPDATE query
- Changed from adding contactId first to adding it LAST (after SET fields)
- Parameters now match SQL order: SET values first, then WHERE value
- Contact fields now properly update in database

### Added
- Debug logging for parameter names and values
- Shows parameter type and value for each field in Turso query

## [1.0.4] - 2026-03-06

### Fixed
- Changed function parameter types from [hashtable] to [System.Collections.IDictionary]
- This preserves [ordered]@{} parameter ordering throughout the call chain
- Fixes contact lookup query failing due to parameter order conversion
- Contacts are now properly updated instead of creating duplicates

## [1.0.3] - 2026-03-06

### Fixed
- Fixed parameter ordering bug in contacts upsert (update.ps1)
- Use [ordered]@{} to guarantee parameter order matches SQL placeholders
- Prevents duplicate contacts when PowerShell hashtable key order is undefined
- Ensures one business and one personal contact per person

## [1.0.2] - 2026-03-04

### Fixed
- Fixed Turso HTTP API response parsing using PSObject.Properties.Match()
- Handle Turso's typed response format: @{type="..."; value="..."}
- Updated Invoke-TursoScalarQuery to properly extract scalar values from typed responses

## [1.0.1] - 2026-03-04

### Fixed
- Removed unsupported [decimal] and [float] type checks for PowerShell 5.1 compatibility
- Added IsNullOrWhiteSpace check for fieldsToExclude configuration

## [1.0.0] - 2026-03-04

### Added
- Initial release of Turso target connector
- Direct Turso HTTP API integration (libsql:// and https:// URLs)
- JWT token authentication (read+write token required)
- Create/correlation script (correlates existing persons by external_id)
- Update script (updates persons, custom_fields, and contacts)
- Field mapping: persons_*, persons_custom_field_*, contacts_*
- Upsert pattern for contacts (query → update or insert)
- Dry run support
