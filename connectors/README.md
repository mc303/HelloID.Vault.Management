# HelloID Vault Management Connectors

A collection of HelloID provisioning connectors for reading and writing data to Vault Management databases. This project provides source connectors (read) and target connectors (write) for SQLite, PostgreSQL, and Supabase databases.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Setup Guide by Database Type](#setup-guide-by-database-type)
  - [SQLite Setup](#31-sqlite-setup)
  - [PostgreSQL Setup](#32-postgresql-setup)
  - [Supabase Setup](#33-supabase-setup)
- [Field Mapping](#field-mapping)
- [Correlation Configuration](#correlation-configuration)
- [Troubleshooting](#troubleshooting)
- [File Structure](#file-structure)
- [Security Best Practices](#security-best-practices)

---

## Overview

### Source vs Target Connectors

| Type | Purpose | Direction |
|------|---------|-----------|
| **Source Connectors** | Import persons, departments, and contracts into HelloID | Vault → HelloID |
| **Target Connectors** | Correlate and update contact information in Vault | HelloID → Vault |

### Available Connectors

#### Source Connectors (Read data FROM Vault)

| Connector | Database | Technology |
|-----------|----------|------------|
| `HelloID-Conn-Prov-Source-Vault-SQLite` | SQLite | System.Data.SQLite.dll |
| `HelloID-Conn-Prov-Source-Vault-Postgres-Npgsql` | PostgreSQL | HelloID.PostgreSQL.dll wrapper (embeds Npgsql) |
| `HelloID-Conn-Prov-Source-Vault-Supabase` | Supabase | HelloID.PostgreSQL.dll wrapper (direct DB access) |

#### Target Connectors (Write data TO Vault)

| Connector | Database | Technology |
|-----------|----------|------------|
| `HelloID-Conn-Prov-Target-Vault-SQLite` | SQLite | System.Data.SQLite.dll |
| `HelloID-Conn-Prov-Target-Vault-Postgres-Npgsql` | PostgreSQL | HelloID.PostgreSQL.dll wrapper |
| `HelloID-Conn-Prov-Target-Vault-Supabase` | Supabase | PostgREST API |

---

## Prerequisites

### HelloID Environment

- HelloID tenant with provisioning module
- HelloID agent installed on Windows Server
- PowerShell 5.1 (default on Windows Server)

### Database Requirements

#### SQLite
- SQLite database file with Vault Management schema
- Read/write file system access to database location

#### PostgreSQL
- PostgreSQL 12+ database server
- Network connectivity from HelloID agent to database server
- Database user with SELECT permissions (source) or SELECT/UPDATE permissions (target)

#### Supabase
- Supabase project with `persons` table
- Database password (from Settings > Database)
- Service Role API key for target connector (from Settings > API)

### Build Machine Requirements (PostgreSQL/Supabase only)

To build the HelloID.PostgreSQL.dll wrapper:

| Requirement | Notes |
|-------------|-------|
| Windows OS | Required for .NET Framework build |
| .NET SDK 8.0+ | For building the project |
| Internet access | To download Npgsql from NuGet |
| PowerShell 5.1+ | To run build scripts |

---

## Setup Guide by Database Type

### 3.1 SQLite Setup

SQLite is the simplest option - no server required, just a file-based database.

#### Step 1: Download System.Data.SQLite.dll

1. Visit: https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki
2. Download the "Set for .NET Framework 4.6" (e.g., `sqlite-netFx46-binary-bundle-Win32-20xx-1.0.xxx.0.zip`)
3. Extract and locate `System.Data.SQLite.dll`
4. **Important:** Choose x64 or x86 version matching your PowerShell architecture

To check your PowerShell architecture:
```powershell
[Environment]::Is64BitProcess  # True = x64, False = x86
```

#### Step 2: Deploy DLL to HelloID Server

```powershell
# Create folder
New-Item -Path "C:\HelloID\SourceData" -ItemType Directory -Force

# Copy DLL
Copy-Item "System.Data.SQLite.dll" "C:\HelloID\SourceData\"
```

#### Step 3: Configure Source Connector

Upload `persons.ps1` and `departments.ps1` from the SQLite connector folder to HelloID.

| Configuration Setting | Value |
|----------------------|-------|
| Database Path | `C:\ProgramData\HelloID.Vault.Management\vault.db` |
| SQLite DLL Path | `C:\HelloID\SourceData\System.Data.SQLite.dll` |
| Source System Filter | (optional) `HR-System-A` |
| Include Inactive Contracts | `false` (recommended) |
| Fields to Exclude | (optional) `Source,custom_*` |
| Debug Mode | `false` |

#### Step 4: Configure Target Connector

Upload `create.ps1` and `update.ps1` from the SQLite target connector folder.

| Configuration Setting | Value |
|----------------------|-------|
| SQLite DLL Path | `C:\HelloID\SourceData\System.Data.SQLite.dll` |
| Database Path | `C:\ProgramData\HelloID.Vault.Management\vault.db` |
| Debug Mode | `false` |

---

### 3.2 PostgreSQL Setup

PostgreSQL connectors use a custom wrapper DLL that embeds Npgsql for PowerShell 5.1 compatibility.

#### Step 1: Build the HelloID.PostgreSQL.dll Wrapper

Choose your Npgsql version:

| Version | DLLs | Best For |
|---------|------|----------|
| **8.0.8** (recommended) | 14 | Standard environments, full feature support |
| **4.0.12** (alternative) | 6 | Constrained environments, GAC conflicts |

**On your Windows build machine:**

```powershell
# Navigate to wrapper source
cd src\HelloID.PostgreSQL

# Option 1: Npgsql 8.0.8 (recommended)
.\build.ps1

# Option 2: Npgsql 4.0.12 (for constrained environments)
.\build-4.0.12.ps1
```

Verify the build:
```powershell
dir .\bin\Release\HelloID.PostgreSQL.dll
# Should show file size ~2-4 MB
```

#### Step 2: Deploy Wrapper to HelloID Server

```powershell
# Create folder on HelloID server
New-Item -Path "C:\HelloID\SourceData" -ItemType Directory -Force

# Copy from build machine
Copy-Item ".\bin\Release\HelloID.PostgreSQL.dll" "C:\HelloID\SourceData\"
```

#### Step 3: Configure Source Connector

Upload `persons.ps1` and `departments.ps1` from the PostgreSQL connector folder.

| Configuration Setting | Value |
|----------------------|-------|
| Wrapper DLL Path | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| Host | `db-project.aivencloud.com` or `localhost` |
| Port | `5432` (standard) or custom port (Aiven) |
| Database | `vault` or `defaultdb` |
| Username | Database username |
| Password | Database password |
| Source System Filter | (optional) |
| Include Inactive Contracts | `false` (recommended) |
| Fields to Exclude | (optional) |
| Debug Mode | `false` |

**Connection examples:**

| Environment | Host | Port | Database | Username |
|-------------|------|------|----------|----------|
| Local PostgreSQL | `localhost` | `5432` | `vault` | `postgres` |
| Aiven PostgreSQL | `db-xxx.aivencloud.com` | `25980` | `defaultdb` | `avnadmin` |

#### Step 4: Configure Target Connector

Upload `create.ps1` and `update.ps1` from the PostgreSQL target connector folder.

| Configuration Setting | Value |
|----------------------|-------|
| Wrapper DLL Path | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| Host | PostgreSQL server hostname |
| Port | `5432` |
| Database | Database name |
| Username | Database username with UPDATE permissions |
| Password | Database password |
| Debug Mode | `false` |

---

### 3.3 Supabase Setup

Supabase is a managed PostgreSQL service. Source connectors use direct database access; target connectors use the PostgREST API.

#### Step 1: Build HelloID.PostgreSQL.dll (Same as PostgreSQL)

Follow [Step 1 of PostgreSQL Setup](#step-1-build-the-helloidpostgresql-wrapper) - the same wrapper DLL is used.

#### Step 2: Get Supabase Credentials

1. Log in to [Supabase Dashboard](https://supabase.com/dashboard)
2. Navigate to **Project Settings** → **Database**
3. Copy these values:
   - **Host**: `db.xxxxxx.supabase.co`
   - **Database**: `postgres` (default)
   - **Password**: Your database password

4. Navigate to **Project Settings** → **API**
5. Copy these values:
   - **URL**: `https://xxxxxxxxxxxx.supabase.co`
   - **service_role key**: (for target connector only - keep secret!)

#### Step 3: Configure Row Level Security (If RLS is Enabled)

If RLS is enabled on your tables, run this in Supabase SQL Editor:

```sql
-- Grant service_role full access
GRANT ALL ON persons TO service_role;
GRANT ALL ON departments TO service_role;

-- Optional: Create explicit policy
CREATE POLICY "Service role can manage persons" 
ON persons FOR ALL 
TO service_role 
USING (true)
WITH CHECK (true);
```

#### Step 4: Configure Source Connector

The Supabase source connector uses direct database access (not API).

| Configuration Setting | Value |
|----------------------|-------|
| Wrapper DLL Path | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| Host | `db.xxxxxx.supabase.co` |
| Port | `6543` (pooler - recommended) or `5432` (direct) |
| Database | `postgres` |
| Username | `postgres` (direct) or `postgres.XXXXXX` (pooler) |
| Password | Database password from dashboard |
| Source System Filter | (optional) |
| Include Inactive Contracts | `false` |
| Fields to Exclude | (optional) |
| Debug Mode | `false` |

**Connection pooler (recommended for production):**
- Port: `6543` (Supavisor session mode)
- Username: `postgres.YOUR_PROJECT_REF` (find in dashboard)
- Benefits: Avoids connection limits, better resource utilization

#### Step 5: Configure Target Connector

The Supabase target connector uses the PostgREST API.

| Configuration Setting | Value |
|----------------------|-------|
| Base URL | `https://xxxxxxxxxxxx.supabase.co` |
| API Key | `service_role` key (NOT anon key) |
| Debug Mode | `false` |

**Important:** Use the `service_role` key, not the `anon` key. The service_role key bypasses RLS and has write access.

---

## Field Mapping

### Source Connector Fields

Source connectors return the following fields for persons:

| Field | Type | Description |
|-------|------|-------------|
| `DisplayName` | String | Full display name |
| `ExternalId` | String | Unique identifier for correlation |
| `given_name` | String | First/given name |
| `family_name` | String | Last/family name |
| `user_name` | String | Username |
| `gender` | String | Gender code |
| `birth_date` | String | Birth date (ISO format) |
| `blocked` | Boolean | Person is blocked |
| `excluded` | Boolean | Person is excluded |
| `contact_business_email` | String | Business email |
| `contact_business_phone_fixed` | String | Business landline |
| `contact_business_phone_mobile` | String | Business mobile |
| `contact_personal_email` | String | Personal email |
| `contact_personal_phone_fixed` | String | Personal landline |
| `contact_personal_phone_mobile` | String | Personal mobile |
| `Contracts` | Array | Nested contract objects |
| `custom_*` | Various | Custom fields from JSON |

### Target Connector Fields (Update Only)

Target connectors can **only update** these contact fields:

| Database Field | HelloID Person Field | Description |
|----------------|---------------------|-------------|
| `business_email` | `Person.Contact.Business.Email` | Business email address |
| `private_email` | `Person.Contact.Personal.Email` | Private email address |
| `phone_fixed` | `Person.Contact.Business.Phone.Fixed` | Business fixed phone |
| `phone_mobile` | `Person.Contact.Business.Phone.Mobile` | Business mobile phone |
| `username` | `Person.Username` | Login username |

### Protected Fields

The following fields **cannot be updated** by target connectors:

| Field | Reason |
|-------|--------|
| `person_id` | Primary key |
| `external_id` | Business/correlation key |
| `PersonId` | HelloID internal reference |
| `ExternalId` | HelloID internal reference |

---

## Correlation Configuration

Target connectors use correlation to match HelloID persons to database records.

### Setting Up Correlation

In HelloID target system configuration:

| Setting | Value |
|---------|-------|
| **Person Field** | `Person.ExternalId` |
| **Account Field** | `external_id` |

### How Correlation Works

1. HelloID passes `Person.ExternalId` to the connector
2. Connector queries the database for matching `external_id`
3. If found: Returns account data for update
4. If not found: Returns "No person found" error

### Important Notes

- **No account creation**: Target connectors only correlate existing persons
- **Update only**: Only contact fields are updated
- **Prerequisite**: Person must exist in database (created by source import or manual entry)

---

## Troubleshooting

### SQLite Issues

#### "Failed to load System.Data.SQLite assembly"

**Cause:** DLL path incorrect or missing.

**Solution:**
```powershell
Test-Path "C:\HelloID\SourceData\System.Data.SQLite.dll"
# Should return: True
```

#### "Unable to load DLL 'SQLite.Interop.dll'"

**Cause:** Architecture mismatch (x86 vs x64).

**Solution:**
1. Verify PowerShell architecture: `[Environment]::Is64BitProcess`
2. Download correct SQLite version (x64 or x86)
3. Ensure DLL matches PowerShell bitness

#### "Database is locked"

**Cause:** Another process has exclusive lock on database.

**Solution:**
1. Close other applications using the database
2. Ensure database uses WAL mode: `PRAGMA journal_mode = WAL;`
3. Check for `.wal` and `.shm` files

### PostgreSQL/Supabase Issues

#### "Wrapper DLL not found"

**Cause:** Incorrect path configuration.

**Solution:**
```powershell
Test-Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"
# Should return: True
```

#### "Unable to load one or more of the requested types"

**Cause:** Assembly conflict in PowerShell 5.1.

**Solution:**
1. Rebuild with Npgsql 4.0.12: `.\build-4.0.12.ps1`
2. Redeploy DLL to HelloID server
3. Verify .NET Framework 4.7.2+ is installed

#### "Could not load file or assembly 'Microsoft.Extensions.*'"

**Cause:** GAC conflict with Microsoft.Extensions.* assemblies.

**Solution:** Use Npgsql 4.0.12 build (fewer dependencies):
```powershell
.\build-4.0.12.ps1
```

#### "Connection refused" or "Connection timeout"

**Cause:** Network connectivity issues.

**Solution:**
```powershell
# Test connectivity
Test-NetConnection -ComputerName "db.server.com" -Port 5432
```
1. Check firewall allows outbound connections
2. Verify PostgreSQL `pg_hba.conf` allows HelloID server IP
3. For Aiven/Supabase: Use correct port from console

#### "too many connections" (Supabase)

**Cause:** Exceeded connection limits.

**Solution:** Use connection pooler (port 6543):
- Port: `6543`
- Username: `postgres.YOUR_PROJECT_REF`

### Supabase API Issues (Target Connector)

#### "401 Unauthorized"

**Cause:** Invalid API key.

**Solution:**
1. Verify using `service_role` key (not `anon` key)
2. Check key is copied correctly (no extra spaces)
3. Regenerate key in Supabase dashboard if needed

#### "403 Forbidden" or "400 Bad Request"

**Cause:** RLS blocking update or column doesn't exist.

**Solution:**
1. Verify column exists in database
2. Run in SQL Editor: `GRANT ALL ON persons TO service_role;`
3. Check Supabase logs for detailed error

#### "No person found in Supabase"

**Cause:** Person doesn't exist in database.

**Solution:**
1. Run source connector first to import persons
2. Verify `external_id` matches between HelloID and database
3. Check correlation configuration

### General Issues

#### Query returns no data

**Cause:** Source filter or filtering options.

**Solution:**
1. Clear "Source System Filter" to import all data
2. Enable "Debug Mode" to see SQL queries
3. Test query directly in database tool

#### PowerShell session crashes

**Cause:** Memory issues or assembly conflicts.

**Solution:**
1. Close and reopen PowerShell
2. Use smaller batch sizes
3. Try alternative Npgsql version (4.0.12)

---

## File Structure

Each connector folder contains these files:

### Source Connector Files

| File | Purpose |
|------|---------|
| `persons.ps1` | Person import script |
| `departments.ps1` | Department import script |
| `configuration.json` | HelloID configuration schema |
| `README.md` | Connector-specific documentation |

### Target Connector Files

| File | Purpose |
|------|---------|
| `create.ps1` | Correlation script (finds existing persons) |
| `update.ps1` | Update script (updates contact fields) |
| `configuration.json` | HelloID configuration schema |
| `fieldMapping.json` | Field mapping for HelloID |
| `README.md` | Connector-specific documentation |

### Wrapper DLL Files (PostgreSQL/Supabase)

Located in `src/HelloID.PostgreSQL/`:

| File | Purpose |
|------|---------|
| `build.ps1` | Build script for Npgsql 8.0.8 |
| `build.bat` | Batch version for 8.0.8 |
| `build-4.0.12.ps1` | Build script for Npgsql 4.0.12 |
| `build-4.0.12.bat` | Batch version for 4.0.12 |
| `Query.cs` | Main wrapper class source code |
| `HelloID.PostgreSQL.csproj` | Project file |

---

## Security Best Practices

### Database Credentials

- **Store credentials in HelloID's encrypted configuration** - Never hardcode passwords
- **Use minimal permissions** - Source connectors need only SELECT; target connectors need SELECT and UPDATE
- **Rotate passwords regularly** - Update both database and HelloID configuration

### Supabase Service Role Key

The `service_role` key has **full database access** and bypasses RLS:

- ⚠️ **Never expose in client-side code or logs**
- ⚠️ **Never commit to version control**
- ⚠️ **Store only in HelloID's encrypted configuration**
- ⚠️ **Regenerate immediately if compromised**

### SQLite Database Security

- Place database files in secure, access-controlled locations
- Use Windows file permissions to restrict access
- Consider SQLCipher for encrypted databases
- Database path is stored in HelloID's encrypted configuration

### Network Security

- Use SSL/TLS for PostgreSQL connections (enabled by default)
- Restrict database server firewall to HelloID agent IP
- Use connection pooler (Supabase port 6543) for production
- Enable connection timeouts to prevent hanging

### Audit and Monitoring

- Enable Debug Mode only during troubleshooting
- Review HelloID logs regularly for anomalies
- Monitor for failed authentication attempts
- Track changes to connector configurations

---

## Support

For issues specific to these connectors:

1. Enable **Debug Mode** and review logs
2. Check the connector-specific README in each connector folder
3. Run diagnostic tests as described in troubleshooting sections
4. Contact Tools4ever support for HelloID platform issues

## Related Documentation

- [HelloID Documentation](https://docs.helloid.com/)
- [Supabase Documentation](https://supabase.com/docs)
- [Npgsql Documentation](https://www.npgsql.org/)
- [System.Data.SQLite](https://system.data.sqlite.org/)
