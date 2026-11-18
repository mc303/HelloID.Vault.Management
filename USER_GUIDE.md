# HelloID Vault Management - User Guide

This guide explains how to use the HelloID Vault Management application, from setting up a database to managing persons and contracts.

---

## Table of Contents

1. [Database Setup](#database-setup)
   - [SQLite (Local)](#sqlite-local)
   - [Supabase (PostgreSQL)](#supabase-postgresql)
   - [Turso (Cloud SQLite)](#turso-cloud-sqlite)
2. [Importing Data](#importing-data)
   - [Full Import](#full-import)
   - [Import Company Data](#import-company-data)
   - [Import with Selection](#import-with-selection)
   - [Anonymization](#anonymization)
3. [Managing Persons](#managing-persons)
   - [Adding a Person](#adding-a-person)
   - [Editing a Person](#editing-a-person)
   - [Deleting a Person](#deleting-a-person)
4. [Managing Contracts](#managing-contracts)
   - [Adding a Contract](#adding-a-contract)
   - [Editing a Contract](#editing-a-contract)
   - [Viewing Contract Details (JSON)](#viewing-contract-details-json)
   - [Searching and Filtering Contracts](#searching-and-filtering-contracts)
5. [Managing Contacts](#managing-contacts)
6. [Reference Data](#reference-data)
7. [Administration](#administration)
   - [Custom Fields](#custom-fields)
   - [Primary Contract Configuration](#primary-contract-configuration)
   - [Primary Manager Administration](#primary-manager-administration)
8. [Navigation](#navigation)

---

## Database Setup

The application supports three database types. Go to **Database Management** in the sidebar to configure your database.

### SQLite (Local)

SQLite is the default database type. No server or account is needed.

1. Select **SQLite** as the database type
2. Choose a database file path (or browse for an existing `.db` file)
3. Click **Save Settings**

The application creates the database file automatically on first import.

### Supabase (PostgreSQL)

Supabase provides a cloud-hosted PostgreSQL database.

1. Create a project at [supabase.com](https://supabase.com)
2. Go to **Settings > Database** in your Supabase project
3. Copy the connection string
4. In the application, select **Supabase** as the database type
5. Paste the connection string into the **Connection String** field
   - You can use either the key-value format or a `postgresql://` URI
6. Optionally enter your Supabase Project URL
7. Click **Test Connection** to verify
8. Click **Save Settings**

**Note:** The schema is created automatically on first import.

### Turso (Cloud SQLite)

Turso provides cloud-hosted SQLite databases accessible via HTTP API.

1. Create an account at [turso.tech](https://turso.tech)
2. In the application, select **Turso** as the database type

**To create a new database:**

3. Go to the **Auto-Create Database** section
4. Enter your Turso **Platform API Token** (from Turso dashboard > Settings)
5. Enter your **Organization Slug** (e.g., `myorg`)
6. Enter a **Database Name** (e.g., `vaultdb`)
7. Click **Create New Database**
8. The application creates the database and fetches the URL and auth token automatically
9. Click **Save Settings**

**To use an existing database:**

3. Enter the **Database URL** (e.g., `libsql://vaultdb-myorg.aws-eu-west-1.turso.io`)
4. Enter the **Auth Token** (from Turso dashboard > your database > Settings > Tokens)
5. Click **Test Connection** to verify
6. Click **Save Settings**

**If a database is unreachable** (zombie database), the application detects this and offers to delete and recreate it automatically.

---

## Importing Data

Data is imported from a `vault.json` file. Go to **Import Data** in the sidebar.

### Full Import

Imports all data from the vault.json: persons, contracts, contacts, departments, and all reference data.

1. Click **Browse** and select your `vault.json` file
2. Choose a **Primary Manager Logic**:
   - **From JSON** — Use manager values as-is from the file
   - **Contract-Based** — Use the primary contract's manager
   - **Department-Based** — Use the department's manager
3. Optionally enable **Anonymize data before import** (see [Anonymization](#anonymization))
4. Click **Import**

If the database already has data, you'll be asked to choose:
- **Backup and Overwrite** — Creates a timestamped backup, then replaces all data
- **Overwrite Without Backup** — Deletes all data and imports fresh
- **Cancel**

### Import Company Data

Imports only reference data (departments, locations, employers, cost centers, teams, divisions, titles, organizations) and custom field schemas. No persons or contracts are imported.

1. Select your `vault.json` file
2. Click **Import Company Data**

This is useful when you want to set up the organizational structure before importing persons.

### Import with Selection

Imports all company data plus only the persons you select.

1. Select your `vault.json` file
2. Click **Import with Selection**
3. A popup appears with all persons from the file:
   - Use the **search box** to filter by name or external ID
   - Check/uncheck persons to select them
   - Use **Select All** or **Deselect All** for convenience
4. Choose import options:
   - **Also import referenced managers (cascade)** — Recursively imports persons who are referenced as managers by your selected persons
   - **Clear manager references for persons not imported** — Sets manager references to NULL if the referenced person was not imported
5. Click **Import Selected**

---

## Anonymization

Anonymization replaces real personal and business data with realistic fake data before import.

### Enabling Anonymization

1. Check **Anonymize data before import** in the Import view
2. The data is anonymized in memory before being written to the database

### What Gets Anonymized

**Personal data:**
- Names (given name, family name, display name, nickname)
- Email addresses
- Phone numbers
- Home addresses
- Birth dates and birth localities
- Usernames
- External IDs

**Business data:**
- Department names
- Location names
- Employer names
- Cost center/bearer names
- Team names
- Title names

### Advanced Options

Click **Advanced Options** to customize:

| Option | Description |
|--------|-------------|
| **Consistent business email domains** | Uses consistent domains for business emails |
| **Generate unique domains per employer** | Each employer gets its own email domain |
| **Custom email domain** | Use a specific domain (e.g., `example.com`) |
| **Duplicate last names** | Control how many people can share a surname |
| **Limit dataset for testing** | Import only N persons (managers auto-included) |
| **Seed** | Fixed seed for reproducible anonymization |

**Duplicate last name modes:**
- **Unlimited** — No restrictions on shared names
- **Unique** — Every person has a unique last name
- **Max 2 / Max 3 / Max 4** — Up to N people can share the same last name
- **Pairs / Triples / Quadruples** — Family group modes (consecutive persons share a name)

---

## Managing Persons

Go to **Persons** in the sidebar.

### Browsing Persons

- The left panel shows a searchable list of persons
- The right panel shows details for the selected person
- Use the search box to find persons by name, external ID, or UUID
- Filter by status: **All / Past / Active / Future**

### Adding a Person

1. Click **Add Person** in the person detail panel
2. Fill in the form:
   - **Display name** (required)
   - **External ID** — Employee number or identifier
   - **Username** — Login name
   - **Source** (required) — Which system this person comes from
   - **Gender** — M, F, or X
   - **Manager** — Click **Search** to find and select a manager
   - Name details, birth date, marital status, etc.
3. Click **Save**

### Editing a Person

1. Select a person from the list
2. Click **Edit Person** in the detail panel
3. Modify the fields
4. Click **Save**

### Deleting a Person

1. Select a person from the list
2. Click **Delete Person**
3. Confirm the deletion

**Note:** Deleting a person also deletes their contracts and contacts (cascade delete).

---

## Managing Contracts

Contracts are managed from two places: the **Contracts** view and the person detail **Contracts** tab.

### Adding a Contract

1. Select a person (either from Persons view or Contracts view)
2. Click **Add Contract**
3. Fill in the form:
   - **External ID** — Auto-generated as `{PersonID}~{GUID}`
   - **Start Date** — Contract start (defaults to today)
   - **End Date** — Contract end (leave empty for open-ended)
   - **Type** — Code and description (e.g., "1 - Personeelslid")
   - **Details** — FTE, hours per week, percentage, sequence
   - **Manager** — Click **Search** to find and select a manager
   - Reference data — Department, Location, Employer, Title, etc. (dropdowns)
4. Click **Save**

### Editing a Contract

1. Go to the person's **Contracts** tab, or use the **Contracts** view
2. Double-click a contract or right-click > **Edit**
3. Modify the fields
4. Click **Save**

### Viewing Contract Details (JSON)

1. In the Contracts view, **double-click** any contract row
2. A window opens showing the contract data formatted as JSON
3. This matches the vault.json structure, including custom fields

### Searching and Filtering Contracts

**Quick Search:**
- Use the search box at the top to search across all fields

**Advanced Search:**
1. Click **Advanced Search**
2. Add filter rows (Field + Operator + Value)
3. Click **Apply Filters**

**Column Visibility:**
1. Click **Columns**
2. Check/uncheck columns to show/hide
3. Use **Select All** or **Hide Empty Columns** for quick adjustments

**Status Filters:**
- **All / Past / Active / Future** — Filter by contract date status

---

## Managing Contacts

Contacts are managed from the person detail **Contacts** tab.

### Adding a Contact

1. Select a person
2. Go to the **Contacts** tab
3. Click **Add Contact**
4. Choose type: **Personal** or **Business**
5. Fill in email, phone, address fields
6. Click **Save**

---

## Reference Data

Reference data represents the organizational structure. All reference data views follow the same pattern: a searchable DataGrid with Add, Edit, and Delete actions.

Available reference data types:

| Type | Description |
|------|-------------|
| **Departments** | Organizational departments with parent hierarchy and manager |
| **Locations** | Physical locations / offices |
| **Cost Centers** | Financial cost centers |
| **Cost Bearers** | Financial cost bearers |
| **Employers** | Legal employers |
| **Teams** | Teams within the organization |
| **Divisions** | Organizational divisions |
| **Titles** | Job titles |
| **Organizations** | Top-level organizations |
| **Source Systems** | Data source systems (tracks usage statistics) |

Each reference data item has an **External ID**, **Code**, **Name**, and **Source** (the source system it belongs to).

---

## Administration

The Administration menu contains three tools:

### Custom Fields

Manage custom field schemas for persons and contracts.

1. Go to **Administration > Custom Fields**
2. View all custom field definitions
3. Filter by table: **All / Persons / Contracts**
4. **Add** a new custom field:
   - Select table (persons or contracts)
   - Enter Field Key (the JSON property name)
   - Enter Display Name (shown in UI)
   - Set Sort Order
5. **Edit** or **Delete** existing fields

Custom field values are stored as JSON in the `custom_fields` column and displayed in the contract JSON view.

### Primary Contract Configuration

Configure which contract is considered the "primary" contract for each person.

1. Go to **Administration > Primary Contract**
2. Arrange priority rules (fields that determine contract ordering):
   - **Contract Status** — Active, Future, Past, No Dates (always first priority)
   - Add fields: FTE, Hours Per Week, Sequence, Start Date, End Date
   - Set sort direction: **Ascending** or **Descending**
   - Reorder with Move Up/Move Down
3. Click **Save**
4. Click **Update Contract Cache** to recalculate all contract statuses
5. Click **Preview** to see how the rules affect actual data

### Primary Manager Administration

Calculate and refresh the primary manager for all persons.

1. Go to **Administration > Primary Manager**
2. View statistics:
   - Total Persons / With Manager / Without Manager
   - Source distribution (Contract-Based, Department-Based, From JSON)
3. **Refresh All Primary Managers:**
   - Choose logic: **Contract-Based** or **Department-Based**
   - Click **Refresh All Primary Managers**
   - This overwrites all existing primary manager values
4. **Recalculate Using Primary Contract Rules:**
   - Applies the configured Primary Contract priority rules to determine each person's primary manager

---

## Navigation

Use the sidebar on the left to navigate between views:

| Section | Items |
|---------|-------|
| **Data** | Persons, Contracts, Contacts |
| **Reference Data** | Departments, Locations, Cost Centers, Cost Bearers, Employers, Teams, Divisions, Titles, Organizations |
| **System** | Source Systems |
| **Administration** | Custom Fields, Primary Contract, Primary Manager |
| **Tools** | Import Data, Database Management |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+1** | Switch to Information tab (Person detail) |
| **Ctrl+2** | Switch to Contracts tab (Person detail) |
| **Ctrl+3** | Switch to Contacts tab (Person detail) |

---

## Tips

- **Switching database types** requires an application restart
- **Import overwrites** all existing data — always use **Backup and Overwrite** when unsure
- **Anonymization** only affects imported data — existing data in the database is not anonymized
- **Custom email domains** are useful for test environments to avoid sending emails to real addresses
- **Limit dataset** option is helpful for quick testing with a small subset of data
- **Manager search** requires at least 2 characters and searches across name, external ID, and username
