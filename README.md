# HelloID Vault Management

A Windows desktop application (WPF, .NET 8) for managing HelloID vault data:
persons, contracts, contacts, reference data, and custom fields — with optional
Active Directory correlation.

## Features

- Import and manage persons, contracts, and contacts
- Active Directory correlation: match vault persons to AD accounts and write
  correlation attributes back to AD
- Custom fields with pivot-table data view
- CSV export from every table view (right-click or the Export button)
- Multiple database backends: SQLite (local), PostgreSQL, Supabase, and Turso
- Anonymization support for test data

## Download

Download the latest installer or ZIP from the
[GitHub Releases](../../releases) page. No elevation is required for normal
use; start the app as administrator only for AD operations that need Domain
Admins rights.

## Quick Start

1. Start the app — it runs in offline mode when no database is reachable
2. Choose a database in **Settings**: SQLite (local file), PostgreSQL,
   Supabase, or Turso
3. Import data via the **Import** view (company data or manual entry)
4. See the [User Guide](USER_GUIDE.md) for detailed instructions

## Documentation

- [User Guide](USER_GUIDE.md) — full walkthrough: database setup, importing,
  managing persons/contracts, reference data, administration
- [Changelog](CHANGELOG.md) — release history

## Building from Source

```bash
dotnet build HelloID.Vault.Management.sln
dotnet run --project src/HelloID.Vault.Management/HelloID.Vault.Management.csproj
```

Requires the .NET 8 SDK (Windows for running the WPF UI).
