# HelloID Turso Target Connector

HelloID target connector for updating person and contact data in a Vault Turso database.

## Overview

This connector uses the Turso HTTP API to update existing person records and contact information in the Vault database. It supports correlation and update operations.

## Prerequisites

### Turso Database
- Turso database with Vault Management schema
- Database URL (`libsql://` or `https://` format)
- Database authentication token (JWT with write permissions)

### Token Creation
1. Log in to [Turso Dashboard](https://turso.tech/dashboard)
2. Select your database
3. Click **Settings** → **Tokens**
4. Create a new token with **Read + Write** permissions
5. Copy the token for connector configuration

### HelloID Environment
- HelloID tenant with provisioning module
- HelloID agent installed on Windows Server
- PowerShell 5.1 (default on Windows Server)
- Network connectivity from HelloID agent to Turso API

## Configuration

### Target Connector Settings

| Setting | Description | Example |
|---------|-------------|---------|
| **Database URL** | Turso database URL | `libsql://vaultdb-org.turso.io` |
| **Auth Token** | Database JWT token (with write permissions) | `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...` |
| **Debug Mode** | Enable verbose logging | `false` |

### Correlation Configuration

| Setting | Value |
|---------|-------|
| **Person Field** | `Person.ExternalId` |
| **Account Field** | `external_id` |

## Operations

### Correlation (create.ps1)

Finds existing persons in the database by `external_id` for correlation.

**Process:**
1. HelloID passes `Person.ExternalId` to connector
2. Connector queries Turso: `SELECT person_id, external_id, given_name, family_name FROM persons WHERE external_id = ?`
3. If found: Returns `AccountReference` with `PersonId`
4. If not found: Throws "No person found" error

**Important Notes:**
- **Correlation only** - Does NOT create new persons
- **Prerequisite** - Person must exist in database (created by source import or manual entry)
- **One match required** - Multiple matches throw error

### Update (update.ps1)

Updates person and contact information for existing correlated persons.

**Supported Update Fields:**

| Field Pattern | Database Table | Example | Notes |
|---------------|-----------------|---------|-------|
| `persons_*` | `persons` | `persons_user_name` | Direct column mapping |
| `persons_custom_field_*` | `persons.custom_fields` | `persons_custom_field_MyField` | Merged into JSON |
| `contacts_*` | `contacts` | `contacts_business_email` | Upsert pattern |

**Field Mapping Examples:**

| HelloID Field | Maps To | Example |
|---------------|---------|---------|
| `persons_user_name` | `persons.user_name` | `jsmith` |
| `persons_custom_field_employee_id` | `persons.custom_fields.employee_id` | `12345` |
| `contacts_business_email` | `contacts.email` (Business) | `john.doe@example.com` |
| `contacts_business_phone_mobile` | `contacts.phone_mobile` (Business) | `+1234567890` |
| `contacts_personal_email` | `contacts.email` (Personal) | `john@gmail.com` |

**Contact Type Mapping:**

The connector automatically extracts contact type from field names:
- `contacts_business_*` → `type = 'Business'`
- `contacts_personal_*` → `type = 'Personal'`

## Protected Fields

The following fields **cannot** be updated:

| Field | Reason |
|-------|--------|
| `person_id` | Primary key |
| `external_id` | Business/correlation key |
| `PersonId` | HelloID internal reference |
| `ExternalId` | HelloID internal reference |

## Field Naming Convention

### Persons Table

**Format:** `persons_<column_name>`

| HelloID Field | Database Column |
|---------------|-----------------|
| `persons_user_name` | `user_name` |
| `persons_given_name` | `given_name` |
| `persons_family_name` | `family_name` |
| `persons_gender` | `gender` |

### Custom Fields

**Format:** `persons_custom_field_<field_name>`

| HelloID Field | Database Storage |
|---------------|------------------|
| `persons_custom_field_employee_id` | `custom_fields.employee_id` |
| `persons_custom_field_department` | `custom_fields.department` |

### Contacts Table

**Format:** `contacts_<contact_type>_<field_name>`

| HelloID Field | Maps To |
|---------------|---------|
| `contacts_business_email` | `contacts.email` WHERE `type = 'Business'` |
| `contacts_business_phone_mobile` | `contacts.phone_mobile` WHERE `type = 'Business'` |
| `contacts_personal_email` | `contacts.email` WHERE `type = 'Personal'` |

## Upsert Behavior

The connector uses an **upsert pattern** for contacts:

1. **Query** for existing contact by `person_id` and `type`
2. **If found:** Update existing contact record
3. **If not found:** Insert new contact record

This ensures:
- No duplicate contacts per type
- Automatic contact creation for new types
- Idempotent updates (safe to retry)

## Troubleshooting

### "Authentication failed"
**Cause:** Invalid token or insufficient permissions.

**Solution:**
1. Verify token has **Read + Write** permissions
2. Check token expiration in Turso dashboard
3. Regenerate token with correct permissions

### "No person found in Turso database"
**Cause:** Person doesn't exist in database.

**Solution:**
1. Run source connector first to import persons
2. Verify `external_id` matches between HelloID and database
3. Check correlation configuration

### "No rows updated in persons table"
**Cause:** Person doesn't exist or no updateable fields.

**Solution:**
1. Verify `person_id` from correlation exists in database
2. Ensure at least one `persons_*` field is mapped
3. Check field names match database schema

### "Turso query error: no such table"
**Cause:** Database schema not initialized.

**Solution:**
1. Ensure Vault Management schema is applied to Turso database
2. Use HelloID.Vault.Management app to import vault.json

### "Contact field not updated"
**Cause:** Field name doesn't match database schema.

**Solution:**
1. Check database schema: `SELECT * FROM contacts LIMIT 1`
2. Verify field name is correct (e.g., `phone_mobile`, not `mobile`)
3. Check field mapping in HelloID configuration

## Dry Run Mode

For testing, the connector supports dry run mode:

1. Enable **Debug Mode** in connector configuration
2. Set **Dry Run** in HelloID target system configuration
3. Connector will log changes without executing them
4. Review logs before disabling dry run

## Performance Considerations

### HTTP Latency
Each UPDATE/INSERT requires an HTTP request to Turso:

- **Person update:** ~50-150ms
- **Contact upsert:** ~50-150ms each
- **Total per person:** ~100-300ms (1 person + 2 contacts)

### Batch Updates
For bulk updates:
- Process in smaller batches (50-100 persons)
- Use during off-peak hours
- Monitor Turso quota limits

## Security Best Practices

### Token Management
- ✅ Use read+write tokens only for target connectors
- ✅ Store token in HelloID's encrypted configuration
- ✅ Rotate tokens regularly (30-90 days recommended)
- ❌ Never use read-only tokens for target connectors
- ❌ Never hardcode tokens in scripts

### Write Permissions
Target connectors require **write access** to:
- `persons` table (UPDATE)
- `persons.custom_fields` JSON column
- `contacts` table (INSERT/UPDATE)

Use principle of least privilege - create separate tokens for source and target.

### Audit Trail
Enable HelloID audit logs to track:
- Which persons were updated
- Which fields were modified
- Update timestamps
- Failed update attempts

## Related Documentation

- [Turso Documentation](https://turso.tech/docs)
- [Turso HTTP API](https://turso.tech/docs/http-api)
- [HelloID Documentation](https://docs.helloid.com/)
- [Vault Management README](../README.md)

## Support

For issues specific to this connector:
1. Enable **Debug Mode** and review logs
2. Check Turso dashboard for database status
3. Verify correlation configuration
4. Contact Tools4ever support for HelloID platform issues
