# HelloID Turso Source Connectors

HelloID source connectors for reading person and department data from a Vault Turso database.

## Overview

These connectors use the Turso HTTP API to query Vault database tables via SQL. They return person and department data in HelloID-compatible format.

## Prerequisites

### Turso Database
- Turso database with Vault Management schema
- Database URL (`libsql://` or `https://` format)
- Database authentication token (JWT)

### Token Creation
1. Log in to [Turso Dashboard](https://turso.tech/dashboard)
2. Select your database
3. Click **Settings** → **Tokens**
4. Create a new token with **Read** permissions
5. Copy the token for connector configuration

### HelloID Environment
- HelloID tenant with provisioning module
- HelloID agent installed on Windows Server
- PowerShell 5.1 (default on Windows Server)
- Network connectivity from HelloID agent to Turso API

## Configuration

### Source Connector Settings

| Setting | Description | Example |
|---------|-------------|---------|
| **Database URL** | Turso database URL | `libsql://vaultdb-org.turso.io` |
| **Auth Token** | Database JWT token | `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...` |
| **Source System Filter** | Optional filter by source | `HR-System-A` |
| **Include Inactive Contracts** | Include expired contracts | `false` (recommended) |
| **Fields to Exclude** | Comma-separated fields to exclude | `Source,custom_*` |
| **Debug Mode** | Enable verbose logging | `false` |

### Database URL Formats

| Format | Example | Notes |
|--------|---------|-------|
| **libsql://** | `libsql://vaultdb-org.turso.io` | Turso format (recommended) |
| **https://** | `https://vaultdb-org.turso.io` | HTTP format |

## Connector Scripts

### persons.ps1
Imports persons with their contracts and contacts.

**Fields returned:**
- `DisplayName`, `ExternalId` (HelloID correlation fields)
- `given_name`, `family_name`, `user_name`
- `gender`, `birth_date`, `marital_status`
- `blocked`, `excluded`
- `Contracts` (array of contract objects)
- `contact_business_email`, `contact_business_phone_mobile`
- `contact_personal_email`, `contact_personal_phone_mobile`
- `custom_*` (expanded from custom_fields JSON)

### departments.ps1
Imports departments with hierarchy information.

**Fields returned:**
- `DisplayName`, `ExternalId` (HelloID correlation fields)
- `code`, `parent_external_id`
- `manager_person_id`, `ManagerExternalId`, `manager_display_name`
- `source`

## Field Mapping

### Person Fields

| HelloID Field | Database Column | Description |
|---------------|-----------------|-------------|
| `DisplayName` | `display_name` | Full display name |
| `ExternalId` | `external_id` | Unique identifier |
| `given_name` | `given_name` | First/given name |
| `family_name` | `family_name` | Last/family name |
| `user_name` | `user_name` | Login username |
| `Contracts` | (joined) | Contract array |

### Contract Fields (Nested)

| Field | Description |
|-------|-------------|
| `contract_external_id` | Contract external ID |
| `contract_start_date` | Start date (ISO format) |
| `contract_end_date` | End date (ISO format) |
| `contract_type_description` | Contract type name |
| `department_display_name` | Department name |
| `title_name` | Job title name |

### Department Fields

| HelloID Field | Database Column | Description |
|---------------|-----------------|-------------|
| `DisplayName` | `display_name` | Department name |
| `ExternalId` | `external_id` | Department code/ID |
| `code` | `code` | Department code |
| `parent_external_id` | Parent department ID |

## Troubleshooting

### "Authentication failed"
**Cause:** Invalid or expired JWT token.

**Solution:**
1. Verify token is copied correctly (no extra spaces)
2. Check token expiration in Turso dashboard
3. Generate new token if needed

### "Connection timeout"
**Cause:** Network connectivity issues.

**Solution:**
```powershell
Test-NetConnection -ComputerName "your-database-org.turso.io" -Port 443
```

### "No data returned"
**Cause:** Source filter or empty database.

**Solution:**
1. Clear "Source System Filter" to import all data
2. Enable "Debug Mode" to see SQL queries
3. Verify database has data using Turso CLI

### "Turso query error: no such table"
**Cause:** Database schema not initialized.

**Solution:**
1. Ensure Vault Management schema is applied to Turso database
2. Use HelloID.Vault.Management app to import vault.json

### PowerShell session crashes
**Cause:** Memory issues with large datasets.

**Solution:**
1. Enable "Source System Filter" to reduce data
2. Close and reopen PowerShell
3. Consider exporting smaller batches

## Performance Considerations

### Query Optimization
The `person_details_view` uses complex window functions. For large datasets:

1. **Use source filter** - Reduces data significantly
2. **Filter inactive contracts** - Excludes expired contracts
3. **Debug mode off** - Reduces logging overhead

### HTTP Latency
Each query requires an HTTP request to Turso:

- **Typical latency:** 50-200ms per query
- **Persons query:** ~100-500ms (depends on row count)
- **Contracts query:** ~200-1000ms (larger result set)
- **Total import time:** Varies by dataset size

### Recommendations
- Use source filtering when possible
- Import during off-peak hours
- Monitor Turso quota limits

## Security Best Practices

### Token Management
- ✅ Store token in HelloID's encrypted configuration
- ✅ Use read-only tokens for source connectors
- ✅ Rotate tokens regularly (Turso recommends 30-90 days)
- ❌ Never hardcode tokens in scripts
- ❌ Never commit tokens to version control

### Network Security
- Use HTTPS (implicit with libsql:// URLs)
- Restrict token permissions (read-only for source)
- Monitor token usage in Turso dashboard
- Revoke unused tokens immediately

## Related Documentation

- [Turso Documentation](https://turso.tech/docs)
- [Turso HTTP API](https://turso.tech/docs/http-api)
- [HelloID Documentation](https://docs.helloid.com/)
- [Vault Management README](../README.md)

## Support

For issues specific to these connectors:
1. Enable **Debug Mode** and review logs
2. Check Turso dashboard for database status
3. Verify network connectivity from HelloID server
4. Contact Tools4ever support for HelloID platform issues
