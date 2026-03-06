# Changelog - HelloID-Conn-Prov-Target-Vault-SQLite

All notable changes to this connector will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).
    Dates should be in YYYY-MM-DD format.

## [1.0.0] - 2026-02-14

### Changed
- Renamed connector by removing -MicrosoftData suffix
- Upgraded to Microsoft.Data.Sqlite (SQLitePCLRaw)
- Deprecated old System.Data.SQLite connector

## [0.9.0] - 2026-02-10

### Added
- Version numbers added to target connector scripts
- Updated with new field mapping convention
- Exclude protected fields from update (person_id, external_id, PersonId)

## [0.2.0] - 2026-02-05

### Changed
- Updated configuration.json and fieldMapping.json to V2 template best practices
- Use database column names directly in field mapping

## [0.1.0] - 2026-02-01

### Added
- Initial release of SQLite target connector
- Create/correlation and update scripts
- Field mapping support
- Dry run support
- Microsoft.Data.Sqlite driver support via HelloID.SQLite wrapper
