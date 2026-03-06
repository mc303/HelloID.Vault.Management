# Changelog - HelloID-Conn-Prov-Source-Vault-SQLite
All notable changes to this connector will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [1.0.0] - 2026-02-14

### Changed
- Renamed connector by removing -MicrosoftData suffix
- Upgraded to Microsoft.Data.Sqlite (SQLitePCLRaw)
- Deprecated old System.Data.SQLite connector

## [0.9.0] - 2026-02-10

### Added
- Comprehensive README documentation
- Source filtering support
- Field exclusion support
- Person import with contracts and contacts
- Department import with manager info

## [0.1.0] - 2026-02-01

### Added
- Initial release of SQLite source connector
- Microsoft.Data.Sqlite driver support via HelloID.SQLite wrapper
