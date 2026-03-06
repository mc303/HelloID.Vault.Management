# Changelog - HelloID-Conn-Prov-Source-Vault-Supabase
All notable changes to this connector will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/).
    Dates should be in YYYY-MM-DD format.

## [1.1.0] - 2026-02-22

### Changed
- Match field naming with PostgreSQL-Npgsql connector
- Add contract_ prefix to contract base fields
- Initialize all expected contract fields with null values
- Include all reference table fields in contract output
- Change ManagerExternalId to manager_external_id in departments
- Remove manager_person_id from departments output

## [1.0.0] - 2026-02-14

### Added
- Initial release of Supabase source connector
- Supabase PostgreSQL database integration
- Connection string authentication
- Person import with contracts and contacts
- Department import with manager info
- Source filtering support
- Field exclusion support
