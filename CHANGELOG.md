# Changelog

All notable changes to HelloID.Vault.Management will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.1] - 2026-09-03

### Added
- Start app in offline mode when database is unreachable

### Fixed
- Custom Fields pivot fails on DataTable.Load type inference
- Reload Custom Field views after import
- Use CHANGELOG.md content for GitHub release notes

### Changed
- Remove empty Unreleased section from CHANGELOG
- Remove internal tooling section from public changelog

## [0.5.0] - 2026-07-17

### Added
- Add Advanced Search to Custom Fields view
- Add Reset Settings button to Custom Fields view
- Remember selected row in Custom Fields view across navigation
- Make Custom Fields view state sticky across navigation
- Add Custom Field Data view with pivot table display

### Fixed
- Case-insensitive search on Persons and Contracts views
- Case-insensitive search for Unicode characters on SQLite/Turso
- Make search and advanced filters case-insensitive
- Recreate columns and restore data when returning to Custom Fields view
- Improve search UX - 500ms debounce, no flicker, no overlay on refinement
- Show total record count from database instead of loaded row count
- Wire toggle button commands and match search bar size to ContactsView
- Fix incomplete trigger and remove orphaned FTS trigger in Turso schema

### Changed
- Split Custom Field Data into separate Person and Contract views
- Rename Custom Field Data to Custom Fields
- Redesign Custom Field Data view to match Contracts/Contacts layout

## [0.4.2] - 2026-07-14

### Added
- Add custom External ID range for anonymization

### Fixed
- Rename wrapper namespaces to match PowerShell connector scripts

### Changed
- Use auto-generated release notes for GitHub releases

## [0.4.1] - 2026-07-13

### Fixed
- Handle empty string dates for PostgreSQL
- Align coverpage buttons size and layout
- Use HTML buttons for coverpage navigation with proper styling
- Fix coverpage button links
- Add .nojekyll to serve _sidebar.md and _coverpage.md

### Changed
- branch 'main' of https://github.com/mc303/HelloID.Vault.Management.DEV
- Add docs-pages folder and sync-docs script for pages site
- Use same background color for both coverpage buttons
- Initial clean Docsify documentation site
- Update repo links back to mc303
- Update repo links to HelloID-Vault-Management org
- Update repo links to public repo (mc303/HelloID.Vault.Management)
- Point docs to external Pages repo, remove failed workflow
- Add Docsify documentation site with GitHub Pages

## [0.4.0] - 2026-07-11

### Added
- Add manager search, import with selection, and CRUD tests
- Add configurable name sharing modes for anonymization
- Enforce unique names for all reference data types
- Add unique last name enforcement and foreign name options
- Add foreign name percentage option for anonymization
- Add UI integration for vault anonymization (Phase 2)
- Complete Phase 1 - VaultAnonymizerService implementation
- Add remaining business data anonymizers
- Add vault.json anonymization feature (Phase 1 - Core Service)
- Add vault.json anonymization feature (Phase 1 - Core Service)
- Add Turso database connectors for HelloID provisioning
- Add password reveal buttons to Settings view
- Add Turso database support

### Fixed
- Resolve merge conflicts - use feature branch anonymization files
- Contract JSON view for PostgreSQL and add custom fields display
- Remove duplicate Default value from ForeignNameMix enum
- Anonymize root-level Departments array with consistent IDs
- Fix 5 critical anonymization bugs
- Improve anonymization consistency and fix dictionary key bug
- Exclude HelloID.Vault.Tests.sln from production deployment
- Fix parameter ordering bug in Turso target connector update.ps1
- Turso database creation and import workflow (v1.0.8)
- Turso connector null value handling (v1.0.7)
- Extract value from Turso's typed response format @{type, value}
- Filter empty external_id strings and add fallback type conversion
- Use string keys for person_id hashtable lookups in Turso persons.ps1
- Handle null excludedFields and skip invalid person records
- Add null checks for person.person_id in lookup and warning log
- Add null checks for person_id in Turso persons.ps1 grouping
- Remove unsupported [decimal] and [float] type checks from Turso connectors
- Add custom field schema import to Turso import path
- Add missing fields to contacts and departments imports
- Include all person fields in Turso import
- Turso database improvements and contract import fixes
- Add UploadDatabaseAsync and Turso import error message
- Turso serialization and add Database initializer registration
- Handle Dictionary parameters in TursoClient ConvertParameters
- Add TursoDatabaseConnectionFactory and repository-based contract queries
- Handle 'nothing to commit' gracefully in deploy-production script

### Changed
- branch 'main' of https://github.com/mc303/HelloID.Vault.Management.DEV
- Merge feature/vault-anonymization into main
- Optimize PostgreSQL import from 142s to ~14s via COPY and batching
- Remove Arabic locale from foreign name options
- Add unit tests for VaultAnonymizerService (Phase 3)
- Update AGENTS.md with solution file clarification
- Separate test projects into dedicated solution file
- branch 'feature/turso-database-support'
- Simplify fieldMapping.json for target connectors
- Add CHANGELOG.md for all Vault connectors
- Add CHANGELOG.md for all Vault connectors
- Add CHANGELOG.md for all Vault connectors
- Remove combined CHANGELOG.md
- Add CHANGELOG.md for all Vault connectors
- Add CHANGELOG.md for all Vault connectors
- Add comprehensive CHANGELOG.md for all Vault connectors
- Bump Turso connectors to v1.0.1 and update CHANGELOG.md

### Added
- Turso database support (cloud-only, REST API-based)
  - HelloID provisioning connectors for Turso database
    - Source connectors: persons.ps1, departments.ps1 (import to HelloID)
    - Target connectors: create.ps1, update.ps1 (correlation + update from HelloID)
    - Direct Turso HTTP API integration (libsql:// and https:// URLs)
    - JWT token authentication (read-only for source, read+write for target)
    - Field naming: persons_*, persons_custom_field_*, contacts_*/
    - Upsert pattern for contacts (query → update or insert)
    - Configuration.json and README.md for both connectors
    - Updated main connectors/README.md with Turso setup section
  - Turso-specific schema (db/turso_schema.sql)
  - Setup documentation (docs/TURSO_SETUP.md)
  - HTTP client with retry logic and automatic token refresh
  - Network status detection and offline warnings
  - Full feature parity with other database options

### Fixed
- Turso connector PowerShell 5.1 compatibility - see connector-specific changelogs:
  - `connectors/HelloID-Conn-Prov-Source-Vault-Turso/CHANGELOG.md`
  - `connectors/HelloID-Conn-Prov-Target-Vault-Turso/CHANGELOG.md`

## [0.3.0] - 2026-02-16

### Added
- Add import strategy pattern for database-specific FK constraint handling
- Add command line interface to VaultImportTestRunner
- Add VaultImportTestRunner console app for import testing
- Add PostgreSQL connection string normalization (URI to key-value)
- Add full PostgreSQL schema conversion and compatibility layer
- Add IPv4 hostname resolution to fix IPv6 timeout issues
- Add comprehensive error logging for connection debugging
- Add debug logging and SSL mode auto-config for Supabase
- Complete Phase 5 - Settings UI for database configuration
- Complete Phase 4 - Repository layer updates for Supabase integration
- Complete Phase 4 - Repository layer updates for Supabase integration
- Add Supabase database integration infrastructure (Phases 1-3)
- Add DatabasePath property to user preferences
- Add vault.json to db folder and exclude language resources

### Fixed
- Database type change restart prompt now triggers correctly
- Validate contract FK references before insert for managed PostgreSQL
- Auto-create departments with correct (external_id, source) FK matching
- Two-pass department import for managed PostgreSQL (Aiven)
- Extract database path from connection string for SQLite factory
- Resolve relative paths from assembly location in VaultImportTestRunner
- Improve database compatibility and transaction handling
- Add configurable schema paths for test runner
- Use factory method for MemoryCache with MemoryCacheOptions
- Add IMemoryCache registration for ReferenceDataService
- Add DEFERRABLE FK and validation for department parent references
- Validate manager references before person import
- Implement two-pass person import for managed PostgreSQL services
- Make session_replication_role optional for managed PostgreSQL services
- Make SourceSystemRepository INSERT OR IGNORE database-agnostic
- Add ss.display_name to all GROUP BY clauses in orphaned_references_report
- Add source_system join to PostgreSQL views
- Cast TEXT date columns to DATE type for comparisons
- Remove duplicate idx_contract_cache_status_dates index
- Remove duplicate contacts table and fix CREATE VIEW syntax
- Correct table creation order respecting all foreign key dependencies
- Reorder postgres_schema.sql tables for correct foreign key dependencies
- Add PostgreSQL schema creation to DatabaseInitializer
- Convert postgres_schema.sql to proper PostgreSQL syntax
- Add IPv4 DNS resolution to PostgreSqlConnectionFactory
- Improve Npgsql connection error handling in Settings UI
- Store database in LocalAppData instead of Program Files
- Only copy specific db files, not entire folder
- Exclude 'db' folder from language resource removal
- Change install folder name to HelloID.Vault.Management
- Remove PublishSingleFile to include all runtime DLLs
- Use relative paths and set x64 architecture for WiX
- Simplify GitHub Release name to version only
- Remove auto tag creation, add tag verification step
- Remove invalid Language attribute from WiX v4 Package element
- Use Subdirectory attribute for WiX v4/v6 subdirectory files
- Include db/sqlite_schema.sql in release artifacts
- Use dynamic git tag in deployment script summary

### Changed
- Use Storage icon for Database Management navigation item
- Move Database Management to bottom of navigation after Import Data
- Rename navigation item from "App Settings" to "Database Management"
- Update CLAUDE.md memory files with recent session activity
- Optimize custom field import with batch JSON updates
- Split VaultImportService into smaller helper classes
- Update CLAUDE.md memory
- Migrate person and contract imports to strategy pattern
- Merge feature/vault-import-test-runner into supabase-integration
- Integrate strategy pattern into VaultImportService for department imports
- Update CLAUDE.md memory
- Replace two-pass import with DEFERRABLE FK constraint
- Add SafeSetSessionReplicationRoleAsync for managed PostgreSQL
- Save current state of Supabase integration work
- Add logging to track CREATE VIEW and FUNCTION statement execution
- Add logging to DatabaseInitializer to trace PostgreSQL schema creation
- Add comprehensive error logging to compare SQLite vs PostgreSQL
- Remove debug logging from PersonRepository
- Update connection string format to PostgreSQL URI
- Add entropy (salt) to DPAPI encryption for improved security
- Exclude CLAUDE.md files from build output
- Add db folder verification step

## [0.2.3] - 2026-01-28

### Added
- Migrate to WiX v6 via dotnet tool
- Use git tag version in WiX Product instead of hardcoded version

### Fixed
- Add UpgradeCode attribute to Package element for WiX v6
- Correct WiX v3 configuration for MSI installer
- Correct SummaryInformation element name (remove space)
- Add Summary Information Template for x64 MSI
- Remove invalid Platform attribute from WiX v3 Product element
- Change InstallScope to perMachine for Program Files access
- Install x64 MSI to Program Files instead of Program Files (x86)

### Changed
- Update CHANGELOG with v0.2.1 release entries
- Update manual-release.yml to match build-release.yml

## [0.2.2] - 2026-01-28

### Added
- Auto-generate changelog from git commits in version-bump.py

### FixedGuid="*"
- Use Guid='*' for auto-generation in WiX v3
- Dynamically generate WiX file list to avoid wildcard error
- Use Chocolatey to install WiX Toolset v3
- Switch to WiX v3 for stable MSI creation
- Correct WiX v4 syntax for Component elements
- Correct WiX v4 configuration for MSI installer

## [0.2.1] - 2026-01-28

### Added
- Add MSI installer to GitHub workflow and remove x86 build
- Add Update Contract Cache button to Primary Contract Rules view
- Improve Recalculate button layout and progress indicator
- Add progress overlay for Recalculate Primary Managers operation
- Add Recalculate Primary Managers button and fix primary manager source tracking
- Complete INotifyDataErrorInfo validation rollout to all Edit ViewModels
- Add global exception handler to Application
- Add IDialogService abstraction for better testability
- Reset column order with Reset Settings in Contracts view
- Add reset settings button to Persons view
- Add manual release workflow

### Fixed
- Show progress ring immediately when button clicked
- Center progress ring in Recalculate button and hide text while running
- Center progress ring in Update Cache button and hide text while running
- Show progress indicator immediately when recalculation starts
- Handle FromJson logic in Recalculate Primary Managers button
- Allow multiple persons with same external_id and source
- Allow duplicate external_id across source systems in persons table
- Use code-behind click handler for Select All button
- Make ShowAllColumnsAsync public for XAML binding
- Select All button now directly sets all visibility properties
- Correct Select All and column persistence in ColumnLayoutManager
- Update Select All button binding after async Task rename
- Correct column visibility lookup in CreateAllColumns
- Correct column property names to match ContractDetailDto
- Create ALL columns initially to support toggling hidden columns
- Direct column visibility update without BindingProxy
- Manually raise PropertyChanged in SetColumnVisibility
- Implement INotifyPropertyChanged on BindingProxy
- Forward nested property path in BindingProxy
- Physically reset DataGrid column DisplayIndex in ResetSettings
- Forward property changes through BindingProxy for DataGrid columns
- Use Icon='Undo' for reset button to match ContractsView
- Use valid Icon='Clear' for reset button
- Preserve .git folder when cleaning production destination
- Add contents:write permission for GitHub releases

### Changed
- Change Primary Contract Config buttons to AppBarButton style
- Change Primary Contract Config buttons to AppBarButton style
- Add icons to Primary Contract Config buttons and match reference data view styling
- Change 'Run Preview' button text to 'Preview' and set Width to Auto
- Right-align Update Contract Cache and Preview cards in Primary Contract Rules view
- Run Recalculate and Update Cache operations on background thread
- Move Recalculate Primary Managers button to Primary Manager Admin view
- Extract utility classes from VaultImportService
- Extract services from VaultImportService for better maintainability
- Add comprehensive logging for Select All button issue
- Apply code quality improvements from csharp-developer analysis
- Mark all high/medium priority improvements as complete
- Extract ColumnLayoutManager and add validation support
- Track remaining code improvements
- Remove 400+ debug logging statements from ViewModels and Views
- Complete IDialogService rollout to all ViewModels
- Consolidate column mapping dictionaries to DataGridConstants
- Add ConfigureAwait(false) to Data/Services layers and optimize filtering
- Remove HashPrefix from source systems feature

## [0.2.0] - 2026-01-20

### Added
- Auto-backfill: New custom fields automatically added to all existing records with null value
- JSON storage: Custom fields now stored as `{"field": "value"}` in `custom_fields` column

### Fixed
- SQLite null handling in json_set() - use null parameter instead of json_null() function
- DeleteValuesAsync bug - was incorrectly using tableName as field key

### Removed
- is_required column from custom_field_schemas table (all fields now optional)
- Obsolete EAV views: person_custom_fields_view, contract_custom_fields_view
- Non-existent UI columns from CustomFieldsView (DataType, DefaultValue, IsRequired)

### Changed
- Updated documentation from "EAV pattern" to "JSON storage"
- Simplified custom field schema (text-only fields, all optional)

## [0.1.0] - 2026-01-15

### Added
- Initial release of HelloID.Vault.Management
- Composite primary key implementation for all reference tables
- Source column support for contracts (8 source columns: location, cost_center, cost_bearer, employer, team, division, title, organization)
- Foreign key constraints with source matching
- 27 composite indexes for query performance
- Database views with source-aware joins
- WPF application with MVVM pattern using CommunityToolkit.Mvvm
- Import functionality for HelloID vault data
- Reference data management (departments, divisions, locations, employers, teams, titles, organizations, cost centers, cost bearers)
- Person and contract management with search and filtering
- Column picker for customizable data grid views
- Custom field support for dynamic data attributes
- Primary contract configuration and preview functionality

### Fixed
- Double initialization in ViewModels and Views (performance improvement: 42% faster)
- Race condition in ContractsView DataLoaded event subscription
- Contract cache missing source columns error

### Technical
- Repository layer with composite key operations (9 reference repositories)
- Service layer with source parameter passing (18 methods)
- 9 source columns added to contracts table
- 8 composite foreign key constraints
- Dapper-based data access
- SQLite database with composite primary keys (external_id, source)
