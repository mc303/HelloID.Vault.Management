# HelloID-Conn-Prov-Target-Vault-Supabase

## Description

HelloID target system connector for Vault Supabase database. This connector correlates existing persons and updates contact information using the Supabase PostgREST API with authenticated user context for Row Level Security (RLS).

## Features

- **Correlation only** - Finds existing persons by `external_id` (no account creation)
- **Updates contact fields** - persons_user_name, contacts_business_*, contacts_personal_*
- **Protected fields** - Cannot update `person_id` or `external_id`
- **RLS compatible** - Uses authenticated user JWT for Row Level Security
- **Dry run support** - Preview changes without executing

## Lifecycle Actions

| Action | Supported | Description |
|--------|-----------|-------------|
| Create | ✅ | Correlates existing person by `external_id` |
| Update | ✅ | Updates contact information |
| Enable | ❌ | Not implemented |
| Disable | ❌ | Not implemented |
| Delete | ❌ | Not implemented |

## Requirements

- Supabase project with `persons` and `contacts` tables
- Publishable (anon) API key (from Supabase Dashboard)
- Service account user created in Supabase Auth
- Row Level Security (RLS) configured

## Installation

### Step 1: Configure Row Level Security (RLS)

⚠️ **Required**: The connector needs both table permissions AND RLS policies.

1. Go to Supabase Dashboard → **SQL Editor**
2. Copy the contents of `supabase-rls-setup.sql` (included in this connector)
3. Paste and click **Run**

This script:
- Grants table permissions to `authenticated` and `anon` roles
- Enables RLS on all tables
- Creates policies for read-only (anon) and full access (authenticated)

<details>
<summary>Click to view: supabase-rls-setup.sql</summary>

```sql
-- ============================================================================
-- Supabase RLS Configuration for HelloID Vault Connector
-- ============================================================================

-- STEP 1: Grant Table Permissions
-- RLS policies only work if the role has table-level permissions.
GRANT ALL ON ALL TABLES IN SCHEMA public TO authenticated;
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO authenticated;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO anon;
GRANT USAGE ON SCHEMA public TO authenticated;
GRANT USAGE ON SCHEMA public TO anon;

-- STEP 2: Enable RLS and Create Policies
DO $$ 
DECLARE 
    r RECORD;
BEGIN 
    FOR r IN (
        SELECT table_schema, table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
    ) 
    LOOP 
        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY;', r.table_schema, r.table_name);
        
        EXECUTE format('DROP POLICY IF EXISTS "api_anon_read" ON %I.%I;', r.table_schema, r.table_name);
        EXECUTE format('CREATE POLICY "api_anon_read" ON %I.%I FOR SELECT TO anon USING (true);', r.table_schema, r.table_name);

        EXECUTE format('DROP POLICY IF EXISTS "api_auth_full" ON %I.%I;', r.table_schema, r.table_name);
        EXECUTE format('CREATE POLICY "api_auth_full" ON %I.%I FOR ALL TO authenticated USING (true) WITH CHECK (true);', r.table_schema, r.table_name);
        
        RAISE NOTICE 'RLS configured for: %.%', r.table_schema, r.table_name;
    END LOOP; 
END $$;
```

</details>

### Step 2: Create Service Account User

1. Go to Supabase Dashboard → **Authentication** → **Users**
2. Click **Add user** → **Create new user**
3. Create a service account (e.g., `helloid-connector@yourdomain.com`)
4. Set a secure password
5. ⚠️ Disable "Auto Confirm User" is NOT checked (user should be confirmed)

### Step 3: Get API Keys

1. Go to Supabase Dashboard → **Settings** → **API**
2. Under **Project API keys**, copy the `anon` **public** key (NOT service_role)

### Step 4: Configure HelloID Target System

1. Create a new PowerShell V2 target system in HelloID
2. Upload the connector scripts:
   - `create.ps1`
   - `update.ps1`
3. Configure the connection:
   - **Base URL**: Your Supabase project URL (e.g., `https://xxxxxxxxxxxx.supabase.co`)
   - **API Key**: The publishable/anon key
   - **Service Account Email**: The email from Step 2
   - **Service Account Password**: The password from Step 2

### Step 5: Configure Field Mapping

Import the `fieldMapping.json` file or configure manually:

| Field Name | Description | Person Field |
|------------|-------------|--------------|
| `persons_user_name` | Username | Person.Username |
| `contacts_business_email` | Business email | Person.Contact.Business.Email |
| `contacts_business_phone_mobile` | Business mobile | Person.Contact.Business.Phone.Mobile |
| `contacts_business_phone_fixed` | Business landline | Person.Contact.Business.Phone.Fixed |
| `contacts_personal_email` | Personal email | Person.Contact.Personal.Email |
| `contacts_personal_phone_mobile` | Personal mobile | Person.Contact.Personal.Phone.Mobile |
| `contacts_personal_phone_fixed` | Personal landline | Person.Contact.Personal.Phone.Fixed |

### Step 6: Configure Correlation

Set up correlation to match HelloID persons to Supabase persons:
- **Person Field**: `Person.ExternalId`
- **Account Field**: `external_id`

## Configuration Reference

```json
[
    {
        "key": "baseUrl",
        "type": "input",
        "templateOptions": {
            "label": "Base URL",
            "description": "Supabase project URL (from Project Settings > API > URL)",
            "required": true
        }
    },
    {
        "key": "apiKey",
        "type": "input",
        "templateOptions": {
            "type": "password",
            "label": "API Key (Publishable/Anon)",
            "description": "Publishable/anon key (from Project Settings > API)",
            "required": true
        }
    },
    {
        "key": "authEmail",
        "type": "input",
        "templateOptions": {
            "label": "Service Account Email",
            "description": "Email of the service account user for RLS authentication",
            "required": true
        }
    },
    {
        "key": "authPassword",
        "type": "input",
        "templateOptions": {
            "type": "password",
            "label": "Service Account Password",
            "description": "Password for the service account user",
            "required": true
        }
    },
    {
        "key": "isDebug",
        "type": "checkbox",
        "templateOptions": {
            "label": "Debug Mode",
            "description": "Enable verbose debug logging (disable in production)"
        }
    }
]
```

## Authentication Flow

The connector authenticates with Supabase using the service account credentials:

1. **POST** `/auth/v1/token?grant_type=password` with email/password
2. Receives JWT `access_token` in response
3. Uses `access_token` in `Authorization: Bearer <token>` header for all API requests
4. RLS policies apply based on the authenticated user's permissions

## Security Notes

### Service Account

- Create a dedicated service account for HelloID (not a real user)
- Use a strong, unique password
- Store credentials securely in HelloID's encrypted configuration
- The service account should have minimal required permissions

### Protected Fields

The following fields cannot be updated:
- `person_id` - Primary key
- `external_id` - Business/correlation key
- `PersonId` - HelloID reference
- `ExternalId` - HelloID reference

## Troubleshooting

### Error: "permission denied for table X"

**Cause**: Missing table-level GRANT permissions.

**Solution**: Run the `supabase-rls-setup.sql` script (Step 1). This grants permissions with:
```sql
GRANT ALL ON ALL TABLES IN SCHEMA public TO authenticated;
```

### Error: "new row violates row-level security policy"

**Cause**: RLS policy doesn't allow the operation.

**Solution**: Verify the `api_auth_full` policy exists:
```sql
SELECT * FROM pg_policies WHERE tablename = 'contacts';
```

### Error: "Authentication failed"

**Cause**: Invalid email/password or user doesn't exist.

**Solution**:
1. Verify the service account user exists in Supabase Auth
2. Check email and password are correct
3. Ensure email is confirmed (user should show as "Confirmed" in dashboard)

### Error: "No person found in Supabase"

**Cause**: The person doesn't exist in the Supabase `persons` table.

**Solution**: 
1. Ensure the source connector has run and created the person
2. Verify the `external_id` matches between HelloID and Supabase
3. Check correlation configuration

## Files in This Connector

| File | Description |
|------|-------------|
| `create.ps1` | Correlates existing persons by external_id |
| `update.ps1` | Updates person and contact information |
| `fieldMapping.json` | Field mapping configuration |
| `configuration.json` | HelloID configuration schema |
| `supabase-rls-setup.sql` | SQL script for RLS setup |
| `README.md` | This documentation |

## API Reference

The connector uses the Supabase PostgREST API:

| Action | Method | Endpoint | Authentication |
|--------|--------|----------|----------------|
| Auth | POST | `/auth/v1/token?grant_type=password` | apikey header |
| Correlate | GET | `/rest/v1/persons` | Bearer JWT |
| Update persons | PATCH | `/rest/v1/persons` | Bearer JWT |
| Query contacts | GET | `/rest/v1/contacts` | Bearer JWT |
| Insert contacts | POST | `/rest/v1/contacts` | Bearer JWT |
| Update contacts | PATCH | `/rest/v1/contacts` | Bearer JWT |

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1.0 | 2026-02-22 | Added RLS authentication with service account, added SQL setup script |
| 1.0.0 | 2026-02-20 | Initial release |

## Related Connectors

- **Source**: `HelloID-Conn-Prov-Source-Vault-Supabase` - Reads persons and departments
- **Target**: This connector - Correlates and updates contact information
