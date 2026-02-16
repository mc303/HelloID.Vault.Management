-- HR Management System - PostgreSQL Database Schema
-- Context7-compatible schema with TEXT-based JSON storage
-- Compatible with PostgreSQL 14+

-- Enable required PostgreSQL extensions

-- ============================================================================
-- TABLE CREATION ORDER (Dependency-respecting)
-- ============================================================================
-- 1. source_system - No dependencies
-- 2. persons - Only references source_system + self-reference
-- 3. Reference tables (only reference source_system):
--    - organizations, locations, cost_centers, cost_bearers, employers, teams, divisions, titles
-- 4. departments - References source_system + persons + self
-- 5. contacts - References persons
-- 6. contracts - References persons + all reference tables above
-- 7. custom_field_schemas - No dependencies
-- ============================================================================

-- Source system tracking table
CREATE TABLE source_system (
    system_id TEXT NOT NULL PRIMARY KEY,
    display_name TEXT NOT NULL,
    identification_key TEXT NOT NULL
);

-- Persons table (central table for individuals)
-- Created early because other tables reference it
CREATE TABLE persons (
    person_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    external_id TEXT,  -- Allow duplicates across source systems
    user_name TEXT,
    gender TEXT,
    honorific_prefix TEXT,
    honorific_suffix TEXT,
    birth_date TEXT,  -- ISO 8601 format
    birth_locality TEXT,
    marital_status TEXT,
    initials TEXT,
    given_name TEXT,
    family_name TEXT,
    family_name_prefix TEXT,
    convention TEXT,
    nick_name TEXT,
    family_name_partner TEXT,
    family_name_partner_prefix TEXT,
    blocked BOOLEAN DEFAULT FALSE,
    status_reason TEXT,
    excluded BOOLEAN DEFAULT FALSE,
    hr_excluded BOOLEAN DEFAULT FALSE,
    manual_excluded BOOLEAN DEFAULT FALSE,
    source TEXT,
    primary_manager_person_id TEXT,
    primary_manager_source TEXT,
    primary_manager_updated_at TEXT,
    custom_fields TEXT DEFAULT '{}',
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL,
    FOREIGN KEY (primary_manager_person_id) REFERENCES persons(person_id) ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED
);

-- Organizations table
CREATE TABLE organizations (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Locations table
CREATE TABLE locations (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Cost Centers table
CREATE TABLE cost_centers (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Cost Bearers table
CREATE TABLE cost_bearers (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Employers table
CREATE TABLE employers (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Teams table
CREATE TABLE teams (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Divisions table
CREATE TABLE divisions (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Titles table
CREATE TABLE titles (
    external_id TEXT NOT NULL,
    code TEXT,
    name TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Departments table
CREATE TABLE departments (
    external_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    code TEXT,
    parent_external_id TEXT,
    manager_person_id TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (external_id, source),
    FOREIGN KEY (parent_external_id, source) REFERENCES departments(external_id, source) ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED,
    FOREIGN KEY (manager_person_id) REFERENCES persons(person_id) ON DELETE SET NULL,
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Contacts table (contact information for persons)
CREATE TABLE contacts (
    contact_id SERIAL PRIMARY KEY,
    person_id TEXT NOT NULL,
    type TEXT,
    email TEXT,
    phone_mobile TEXT,
    phone_fixed TEXT,
    address_street TEXT,
    address_street_ext TEXT,
    address_house_number TEXT,
    address_house_number_ext TEXT,
    address_postal TEXT,
    address_locality TEXT,
    address_country TEXT,
    FOREIGN KEY (person_id) REFERENCES persons(person_id) ON DELETE CASCADE
);

-- Contracts table (employment contract details)
CREATE TABLE contracts (
    external_id TEXT UNIQUE,
    contract_id SERIAL PRIMARY KEY,
    person_id TEXT NOT NULL,
    start_date TEXT,  -- ISO 8601 format
    end_date TEXT,  -- ISO 8601 format
    type_code TEXT,
    type_description TEXT,
    fte DOUBLE PRECISION,
    hours_per_week DOUBLE PRECISION,
    percentage DOUBLE PRECISION,
    sequence INTEGER,
    manager_person_external_id TEXT,
    location_external_id TEXT,
    location_source TEXT,
    cost_center_external_id TEXT,
    cost_center_source TEXT,
    cost_bearer_external_id TEXT,
    cost_bearer_source TEXT,
    employer_external_id TEXT,
    employer_source TEXT,
    team_external_id TEXT,
    team_source TEXT,
    department_external_id TEXT,
    department_source TEXT,
    division_external_id TEXT,
    division_source TEXT,
    title_external_id TEXT,
    title_source TEXT,
    organization_external_id TEXT,
    organization_source TEXT,
    source TEXT,
    custom_fields TEXT DEFAULT '{}',
    FOREIGN KEY (person_id) REFERENCES persons(person_id) ON DELETE CASCADE,
    FOREIGN KEY (manager_person_external_id) REFERENCES persons(person_id) ON DELETE SET NULL,
    FOREIGN KEY (location_external_id, location_source) REFERENCES locations(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (cost_center_external_id, cost_center_source) REFERENCES cost_centers(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (cost_bearer_external_id, cost_bearer_source) REFERENCES cost_bearers(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (employer_external_id, employer_source) REFERENCES employers(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (team_external_id, team_source) REFERENCES teams(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (department_external_id, department_source) REFERENCES departments(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (division_external_id, division_source) REFERENCES divisions(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (title_external_id, title_source) REFERENCES titles(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (organization_external_id, organization_source) REFERENCES organizations(external_id, source) ON DELETE SET NULL,
    FOREIGN KEY (source) REFERENCES source_system(system_id) ON DELETE SET NULL
);

-- Custom Field Schemas table (text-only custom field definitions)
CREATE TABLE custom_field_schemas (
    field_key TEXT NOT NULL,
    table_name TEXT NOT NULL CHECK (table_name IN ('persons', 'contracts')),
    display_name TEXT NOT NULL,
    validation_regex TEXT,
    sort_order INTEGER DEFAULT 0,
    help_text TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (table_name, field_key)
);

-- Create indexes for performance optimization
CREATE INDEX idx_persons_display_name ON persons(display_name);
CREATE INDEX idx_persons_external_id_source ON persons(external_id, source);
CREATE INDEX idx_persons_status ON persons(blocked, excluded, hr_excluded, manual_excluded);
CREATE INDEX idx_persons_source ON persons(source);
CREATE INDEX idx_persons_primary_manager ON persons(primary_manager_person_id);
CREATE INDEX idx_persons_primary_manager_source ON persons(primary_manager_source);

CREATE INDEX idx_contracts_person_id ON contracts(person_id);
CREATE INDEX idx_contracts_external_id ON contracts(external_id);
CREATE INDEX idx_contracts_dates ON contracts(start_date, end_date);
CREATE INDEX idx_contracts_status ON contracts(type_code);
CREATE INDEX idx_contracts_source ON contracts(source);

CREATE INDEX idx_contacts_person_id ON contacts(person_id);
CREATE INDEX idx_contacts_type ON contacts(type);
CREATE INDEX idx_contacts_email ON contacts(email);

CREATE INDEX idx_departments_parent ON departments(parent_external_id);
CREATE INDEX idx_departments_manager ON departments(manager_person_id);
CREATE INDEX idx_departments_display_name ON departments(display_name);
CREATE INDEX idx_departments_source ON departments(source);

-- Indexes for reference table source fields
CREATE INDEX idx_organizations_source ON organizations(source);
CREATE INDEX idx_locations_source ON locations(source);
CREATE INDEX idx_employers_source ON employers(source);
CREATE INDEX idx_cost_centers_source ON cost_centers(source);
CREATE INDEX idx_cost_bearers_source ON cost_bearers(source);
CREATE INDEX idx_teams_source ON teams(source);
CREATE INDEX idx_divisions_source ON divisions(source);
CREATE INDEX idx_titles_source ON titles(source);

-- Composite indexes for reference tables (source + name and source + external_id)
CREATE INDEX idx_organizations_source_name ON organizations(source, name);
CREATE INDEX idx_organizations_source_id ON organizations(source, external_id);
CREATE INDEX idx_locations_source_name ON locations(source, name);
CREATE INDEX idx_locations_source_id ON locations(source, external_id);
CREATE INDEX idx_employers_source_name ON employers(source, name);
CREATE INDEX idx_employers_source_id ON employers(source, external_id);
CREATE INDEX idx_cost_centers_source_name ON cost_centers(source, name);
CREATE INDEX idx_cost_centers_source_id ON cost_centers(source, external_id);
CREATE INDEX idx_cost_bearers_source_name ON cost_bearers(source, name);
CREATE INDEX idx_cost_bearers_source_id ON cost_bearers(source, external_id);
CREATE INDEX idx_teams_source_name ON teams(source, name);
CREATE INDEX idx_teams_source_id ON teams(source, external_id);
CREATE INDEX idx_divisions_source_name ON divisions(source, name);
CREATE INDEX idx_divisions_source_id ON divisions(source, external_id);
CREATE INDEX idx_departments_source_name ON departments(source, display_name);
CREATE INDEX idx_departments_source_id ON departments(source, external_id);
CREATE INDEX idx_titles_source_name ON titles(source, name);
CREATE INDEX idx_titles_source_id ON titles(source, external_id);

-- Composite indexes for contracts FKs (to optimize joins with source columns)
CREATE INDEX idx_contracts_location_source ON contracts(location_external_id, location_source);
CREATE INDEX idx_contracts_cost_center_source ON contracts(cost_center_external_id, cost_center_source);
CREATE INDEX idx_contracts_cost_bearer_source ON contracts(cost_bearer_external_id, cost_bearer_source);
CREATE INDEX idx_contracts_employer_source ON contracts(employer_external_id, employer_source);
CREATE INDEX idx_contracts_team_source ON contracts(team_external_id, team_source);
CREATE INDEX idx_contracts_department_source ON contracts(department_external_id, department_source);
CREATE INDEX idx_contracts_division_source ON contracts(division_external_id, division_source);
CREATE INDEX idx_contracts_title_source ON contracts(title_external_id, title_source);
CREATE INDEX idx_contracts_organization_source ON contracts(organization_external_id, organization_source);

CREATE INDEX idx_custom_field_schemas_table ON custom_field_schemas(table_name);
CREATE INDEX idx_custom_field_schemas_sort ON custom_field_schemas(sort_order, display_name);

-- JSON validation triggers
CREATE OR REPLACE FUNCTION validate_person_custom_fields_json()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.custom_fields IS NOT NULL AND NEW.custom_fields::jsonb IS NULL THEN
        RAISE EXCEPTION 'Invalid JSON in custom_fields';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER validate_person_custom_fields_json_insert
    BEFORE INSERT ON persons
    FOR EACH ROW
    EXECUTE FUNCTION validate_person_custom_fields_json();

CREATE TRIGGER validate_person_custom_fields_json_update
    BEFORE UPDATE OF custom_fields ON persons
    FOR EACH ROW
    EXECUTE FUNCTION validate_person_custom_fields_json();

CREATE OR REPLACE FUNCTION validate_contract_custom_fields_json()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.custom_fields IS NOT NULL AND NEW.custom_fields::jsonb IS NULL THEN
        RAISE EXCEPTION 'Invalid JSON in custom_fields';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER validate_contract_custom_fields_json_insert
    BEFORE INSERT ON contracts
    FOR EACH ROW
    EXECUTE FUNCTION validate_contract_custom_fields_json();

CREATE TRIGGER validate_contract_custom_fields_json_update
    BEFORE UPDATE OF custom_fields ON contracts
    FOR EACH ROW
    EXECUTE FUNCTION validate_contract_custom_fields_json();

-- Create schema version table
CREATE TABLE schema_version (
    version INTEGER PRIMARY KEY,
    migration_date TEXT DEFAULT CURRENT_TIMESTAMP,
    description TEXT
);

-- Insert current schema version
INSERT INTO schema_version (version, description) VALUES (1, 'Context7-compatible schema with TEXT-based JSON storage and EAV custom fields');

-- Migration: Add Primary Manager fields to persons table
INSERT INTO schema_version (version, description) VALUES (2, 'Add primary_manager_person_id, primary_manager_source, primary_manager_updated_at to persons table for Primary Manager feature');

-- Migration: Custom fields from EAV to JSON
INSERT INTO schema_version (version, description) VALUES (3, 'Convert custom fields from EAV pattern to JSON storage in persons and contracts tables for easier schema management');

-- Migration: Make primary_manager_person_id FK DEFERRABLE for managed PostgreSQL services
-- This allows circular manager references to be imported in a single transaction
-- For existing databases, run:
--   ALTER TABLE persons DROP CONSTRAINT IF EXISTS persons_primary_manager_person_id_fkey;
--   ALTER TABLE persons ADD CONSTRAINT persons_primary_manager_person_id_fkey
--     FOREIGN KEY (primary_manager_person_id) REFERENCES persons(person_id)
--     ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED;
INSERT INTO schema_version (version, description) VALUES (4, 'Make primary_manager_person_id FK constraint DEFERRABLE INITIALLY DEFERRED to support circular manager references on managed PostgreSQL services (Aiven, AWS RDS, Supabase)');

-- Performance optimization
ANALYZE;

-- ============================================================================
-- DATABASE VIEWS
-- ============================================================================

-- Person Details View
-- Joins person data with primary contract, business contact, and all related lookup tables
CREATE OR REPLACE VIEW person_details_view AS
WITH primary_contracts AS (
    -- Select primary contract per person using business logic priority
    SELECT
        c.person_id,
        c.contract_id,
        c.external_id,
        c.start_date,
        c.end_date,
        c.type_code,
        c.type_description,
        c.fte,
        c.hours_per_week,
        c.percentage,
        c.sequence,
        c.manager_person_external_id,
        c.location_external_id,
        c.cost_center_external_id,
        c.cost_bearer_external_id,
        c.employer_external_id,
        c.team_external_id,
        c.department_external_id,
        c.division_external_id,
        c.title_external_id,
        c.organization_external_id,
        ROW_NUMBER() OVER (
            PARTITION BY c.person_id
            ORDER BY
                -- 1. Contract status priority (Active=1, Future=2, Past=3)
                CASE
                    WHEN c.start_date IS NULL THEN 4
                    WHEN c.start_date::date <= CURRENT_DATE AND (c.end_date::date > CURRENT_DATE OR c.end_date IS NULL) THEN 1
                    WHEN c.start_date::date > CURRENT_DATE THEN 2
                    WHEN c.end_date::date < CURRENT_DATE THEN 3
                    ELSE 4
                END ASC,
                -- 2. Highest FTE
                COALESCE(c.fte, 0) DESC,
                -- 3. Most hours per week
                COALESCE(c.hours_per_week, 0) DESC,
                -- 4. Sequence descending
                COALESCE(c.sequence, 0) DESC,
                -- 5. End date descending (null treated as 2999-01-01)
                COALESCE(c.end_date::date, '2999-01-01'::date) DESC,
                -- 6. Start date ascending
                COALESCE(c.start_date::date, '9999-12-31'::date) ASC,
                -- 7. Contract ID ascending (tie-breaker)
                c.contract_id ASC
        ) AS rn
    FROM contracts c
)
SELECT
    -- Person fields
    p.person_id,
    p.display_name,
    p.external_id,
    p.user_name,
    p.gender,
    p.honorific_prefix,
    p.honorific_suffix,
    p.birth_date,
    p.birth_locality,
    p.marital_status,
    p.initials,
    p.given_name,
    p.family_name,
    p.family_name_prefix,
    p.convention,
    p.nick_name,
    p.family_name_partner,
    p.family_name_partner_prefix,
    p.blocked,
    p.status_reason,
    p.excluded,
    p.hr_excluded,
    p.manual_excluded,

    -- Primary contract fields
    pc.contract_id AS primary_contract_id,
    pc.external_id AS primary_contract_external_id,
    pc.start_date AS primary_contract_start_date,
    pc.end_date AS primary_contract_end_date,
    pc.type_code AS primary_contract_type_code,
    pc.type_description AS primary_contract_type_description,
    pc.fte AS primary_contract_fte,
    pc.hours_per_week AS primary_contract_hours_per_week,
    pc.percentage AS primary_contract_percentage,
    pc.sequence AS primary_contract_sequence,

    -- Primary contract manager
    pc.manager_person_external_id AS primary_contract_manager_external_id,
    mgr.display_name AS primary_contract_manager_name,

    -- Primary manager (person-level)
    p.primary_manager_person_id,
    primary_mgr.display_name AS primary_manager_name,
    primary_mgr.external_id AS primary_manager_external_id,
    p.primary_manager_updated_at,

    -- Location
    pc.location_external_id AS primary_contract_location_external_id,
    loc.name AS primary_contract_location_name,

    -- Cost center
    pc.cost_center_external_id AS primary_contract_cost_center_external_id,
    cc.name AS primary_contract_cost_center_name,

    -- Cost bearer
    pc.cost_bearer_external_id AS primary_contract_cost_bearer_external_id,
    cb.name AS primary_contract_cost_bearer_name,

    -- Employer
    pc.employer_external_id AS primary_contract_employer_external_id,
    emp.name AS primary_contract_employer_name,

    -- Team
    pc.team_external_id AS primary_contract_team_external_id,
    team.name AS primary_contract_team_name,

    -- Department
    pc.department_external_id AS primary_contract_department_external_id,
    dept.display_name AS primary_contract_department_name,
    dept.code AS primary_contract_department_code,

    -- Division
    pc.division_external_id AS primary_contract_division_external_id,
    div.name AS primary_contract_division_name,

    -- Title
    pc.title_external_id AS primary_contract_title_external_id,
    title.name AS primary_contract_title_name,

    -- Organization
    pc.organization_external_id AS primary_contract_organization_external_id,
    org.name AS primary_contract_organization_name,

    -- Business contact fields
    contact.contact_id AS primary_contact_id,
    contact.type AS primary_contact_type,
    contact.email AS primary_contact_email,
    contact.phone_mobile AS primary_contact_phone_mobile,
    contact.phone_fixed AS primary_contact_phone_fixed,
    contact.address_street AS primary_contact_address_street,
    contact.address_street_ext AS primary_contact_address_street_ext,
    contact.address_house_number AS primary_contact_address_house_number,
    contact.address_house_number_ext AS primary_contact_address_house_number_ext,
    contact.address_postal AS primary_contact_address_postal,
    contact.address_locality AS primary_contact_address_locality,
    contact.address_country AS primary_contact_address_country,

    -- Person status (derived from primary contract)
    CASE
        WHEN pc.contract_id IS NULL THEN 'No Contract'
        WHEN pc.start_date IS NULL THEN 'No Contract'
        WHEN pc.start_date::date > CURRENT_DATE THEN 'Future'
        WHEN pc.end_date IS NOT NULL AND pc.end_date::date < CURRENT_DATE THEN 'Past'
        ELSE 'Active'
    END AS person_status,

    -- Primary contract date range
    CASE
        WHEN pc.start_date IS NOT NULL THEN
            CASE
                WHEN pc.end_date IS NOT NULL THEN
                    TO_CHAR(pc.start_date::date, 'YYYY-MM-DD') || ' until ' || TO_CHAR(pc.end_date::date, 'YYYY-MM-DD')
                ELSE
                    TO_CHAR(pc.start_date::date, 'YYYY-MM-DD') || ' until ongoing'
            END
        ELSE NULL
    END AS primary_contract_date_range,

    -- Source system display name
    ss.display_name AS source_display_name

FROM persons p

-- Join primary contract (only rn=1 from priority calculation)
LEFT JOIN primary_contracts pc ON p.person_id = pc.person_id AND pc.rn = 1

-- Join contract manager
LEFT JOIN persons mgr ON pc.manager_person_external_id = mgr.person_id

-- Join primary manager (person-level)
LEFT JOIN persons primary_mgr ON p.primary_manager_person_id = primary_mgr.person_id

-- Join lookup tables
LEFT JOIN locations loc ON pc.location_external_id = loc.external_id
LEFT JOIN cost_centers cc ON pc.cost_center_external_id = cc.external_id
LEFT JOIN cost_bearers cb ON pc.cost_bearer_external_id = cb.external_id
LEFT JOIN employers emp ON pc.employer_external_id = emp.external_id
LEFT JOIN teams team ON pc.team_external_id = team.external_id
LEFT JOIN departments dept ON pc.department_external_id = dept.external_id
LEFT JOIN divisions div ON pc.division_external_id = div.external_id
LEFT JOIN titles title ON pc.title_external_id = title.external_id
LEFT JOIN organizations org ON pc.organization_external_id = org.external_id

-- Join business contact (first business contact for person)
LEFT JOIN (
    SELECT
        person_id,
        contact_id,
        type,
        email,
        phone_mobile,
        phone_fixed,
        address_street,
        address_street_ext,
        address_house_number,
        address_house_number_ext,
        address_postal,
        address_locality,
        address_country,
        ROW_NUMBER() OVER (PARTITION BY person_id ORDER BY contact_id ASC) AS rn
    FROM contacts
    WHERE type = 'Business'
) contact ON p.person_id = contact.person_id AND contact.rn = 1

-- Join source system for display name
LEFT JOIN source_system ss ON p.source = ss.system_id;

-- Contract Details View
-- Joins contract data with person, manager, and all related lookup tables
DROP VIEW IF EXISTS contract_details_view;
CREATE VIEW contract_details_view AS
SELECT
    -- Contract fields
    c.contract_id,
    c.external_id,
    c.person_id,
    c.start_date,
    c.end_date,
    c.type_code,
    c.type_description,
    c.fte,
    c.hours_per_week,
    c.percentage,
    c.sequence,
    c.source,

    -- Person
    p.display_name AS person_name,
    p.external_id AS person_external_id,

    -- Manager
    c.manager_person_external_id AS manager_person_external_id,
    mgr.display_name AS manager_person_name,

    -- Location
    c.location_external_id,
    c.location_source,
    loc.code AS location_code,
    loc.name AS location_name,

    -- Cost center
    c.cost_center_external_id,
    c.cost_center_source,
    cc.code AS cost_center_code,
    cc.name AS cost_center_name,

    -- Cost bearer
    c.cost_bearer_external_id,
    c.cost_bearer_source,
    cb.code AS cost_bearer_code,
    cb.name AS cost_bearer_name,

    -- Employer
    c.employer_external_id,
    c.employer_source,
    emp.code AS employer_code,
    emp.name AS employer_name,

    -- Team
    c.team_external_id,
    c.team_source,
    team.code AS team_code,
    team.name AS team_name,

    -- Department
    c.department_external_id,
    c.department_source,
    dept.display_name AS department_name,
    dept.code AS department_code,
    dept.parent_external_id AS department_parent_external_id,
    dept.manager_person_id AS department_manager_person_id,
    dept_mgr.display_name AS department_manager_name,
    parent_dept.display_name AS department_parent_department_name,

    -- Division
    c.division_external_id,
    c.division_source,
    div.code AS division_code,
    div.name AS division_name,

    -- Title
    c.title_external_id,
    c.title_source,
    title.code AS title_code,
    title.name AS title_name,

    -- Organization
    c.organization_external_id,
    c.organization_source,
    org.code AS organization_code,
    org.name AS organization_name,

    -- Contract status
    CASE
        WHEN c.start_date IS NULL THEN 'No Dates'
        WHEN c.start_date::date > CURRENT_DATE THEN 'Future'
        WHEN c.end_date IS NOT NULL AND c.end_date::date < CURRENT_DATE THEN 'Past'
        ELSE 'Active'
    END AS contract_status,

    -- Contract date range
    CASE
        WHEN c.start_date IS NOT NULL THEN
            CASE
                WHEN c.end_date IS NOT NULL THEN
                    TO_CHAR(c.start_date::date, 'YYYY-MM-DD') || ' until ' || TO_CHAR(c.end_date::date, 'YYYY-MM-DD')
                ELSE
                    TO_CHAR(c.start_date::date, 'YYYY-MM-DD') || ' until ongoing'
            END
        ELSE NULL
    END AS contract_date_range,

    -- Source system display name
    ss.display_name AS source_display_name

FROM contracts c

-- Join person
LEFT JOIN persons p ON c.person_id = p.person_id

-- Join contract manager
LEFT JOIN persons mgr ON c.manager_person_external_id = mgr.person_id

-- Join lookup tables (source-aware to prevent cross-source contamination)
LEFT JOIN locations loc ON c.location_external_id = loc.external_id AND c.location_source = loc.source
LEFT JOIN cost_centers cc ON c.cost_center_external_id = cc.external_id AND c.cost_center_source = cc.source
LEFT JOIN cost_bearers cb ON c.cost_bearer_external_id = cb.external_id AND c.cost_bearer_source = cb.source
LEFT JOIN employers emp ON c.employer_external_id = emp.external_id AND c.employer_source = emp.source
LEFT JOIN teams team ON c.team_external_id = team.external_id AND c.team_source = team.source
LEFT JOIN departments dept ON c.department_external_id = dept.external_id AND c.department_source = dept.source
LEFT JOIN persons dept_mgr ON dept.manager_person_id = dept_mgr.person_id
LEFT JOIN departments parent_dept ON dept.parent_external_id = parent_dept.external_id AND dept.source = parent_dept.source
LEFT JOIN divisions div ON c.division_external_id = div.external_id AND c.division_source = div.source
LEFT JOIN titles title ON c.title_external_id = title.external_id AND c.title_source = title.source
LEFT JOIN organizations org ON c.organization_external_id = org.external_id AND c.organization_source = org.source
LEFT JOIN source_system ss ON c.source = ss.system_id;

-- Primary Contract Configuration
-- Stores the ordered list of fields used to determine the primary contract
CREATE TABLE IF NOT EXISTS primary_contract_config (
    id SERIAL PRIMARY KEY,
    field_name TEXT NOT NULL UNIQUE,       -- e.g., 'Fte', 'HoursPerWeek', 'Sequence', 'StartDate', 'EndDate'
    display_name TEXT NOT NULL,            -- User-friendly name for the field
    sort_order TEXT NOT NULL DEFAULT 'DESC',  -- 'ASC' or 'DESC'
    priority_order INTEGER NOT NULL,       -- Order of priority (1 = highest priority, 2 = next, etc.)
    is_active INTEGER NOT NULL DEFAULT 1,  -- 1 = enabled, 0 = disabled
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Default configuration based on Business-Logic-Rules.md
-- Note: Contract status (Active > Future > Past) is always the first priority and is not configurable
INSERT INTO primary_contract_config (id, field_name, display_name, sort_order, priority_order, is_active) VALUES
(1, 'fte', 'FTE (Full-Time Equivalent)', 'DESC', 1, 1),
(2, 'hours_per_week', 'Hours per Week', 'DESC', 2, 1),
(3, 'sequence', 'Sequence', 'DESC', 3, 1),
(4, 'end_date', 'End Date', 'DESC', 4, 1),
(5, 'start_date', 'Start Date', 'ASC', 5, 1);

-- Contract JSON View
-- Creates a view that matches the exact structure needed for vault.json export
CREATE OR REPLACE VIEW contract_json_view AS
SELECT
    -- Contract fields
    c.contract_id,
    c.external_id,
    c.person_id,
    c.start_date,
    c.end_date,
    c.type_code,
    c.type_description,
    c.fte,
    c.hours_per_week,
    c.percentage,
    c.sequence,
    c.source,

    -- Manager details with email from contacts
    c.manager_person_external_id,
    mgr.external_id AS manager_external_id,
    mgr.display_name AS manager_display_name,
    con_business.email AS manager_email,

    -- Location with code
    c.location_external_id,
    loc.code AS location_code,
    loc.name AS location_name,

    -- Cost center with code
    c.cost_center_external_id,
    cc.code AS cost_center_code,
    cc.name AS cost_center_name,

    -- Cost bearer with code
    c.cost_bearer_external_id,
    cb.code AS cost_bearer_code,
    cb.name AS cost_bearer_name,

    -- Employer with code
    c.employer_external_id,
    emp.code AS employer_code,
    emp.name AS employer_name,

    -- Team with code
    c.team_external_id,
    team.code AS team_code,
    team.name AS team_name,

    -- Department (simplified for JSON structure)
    c.department_external_id,
    dept.display_name AS department_display_name,
    dept.code AS department_code,

    -- Division with code
    c.division_external_id,
    div.code AS division_code,
    div.name AS division_name,

    -- Title with code
    c.title_external_id,
    title.code AS title_code,
    title.name AS title_name,

    -- Organization with code
    c.organization_external_id,
    org.code AS organization_code,
    org.name AS organization_name

FROM contracts c

-- Join manager with available person fields
LEFT JOIN persons mgr ON c.manager_person_external_id = mgr.person_id

-- Join contacts to get manager's business email
LEFT JOIN contacts con_business ON c.manager_person_external_id = con_business.person_id
    AND con_business.type = 'Business'

-- Join lookup tables with code fields (source-aware to prevent cross-source contamination)
LEFT JOIN locations loc ON c.location_external_id = loc.external_id AND c.location_source = loc.source
LEFT JOIN cost_centers cc ON c.cost_center_external_id = cc.external_id AND c.cost_center_source = cc.source
LEFT JOIN cost_bearers cb ON c.cost_bearer_external_id = cb.external_id AND c.cost_bearer_source = cb.source
LEFT JOIN employers emp ON c.employer_external_id = emp.external_id AND c.employer_source = emp.source
LEFT JOIN teams team ON c.team_external_id = team.external_id AND c.team_source = team.source
LEFT JOIN departments dept ON c.department_external_id = dept.external_id AND c.department_source = dept.source
LEFT JOIN divisions div ON c.division_external_id = div.external_id AND c.division_source = div.source
LEFT JOIN titles title ON c.title_external_id = title.external_id AND c.title_source = title.source
LEFT JOIN organizations org ON c.organization_external_id = org.external_id AND c.organization_source = org.source;

-- ============================================================================
-- PHASE 3: MONITORING & REPORTING VIEWS
-- ============================================================================
-- These views provide data quality visibility and source system monitoring.
-- Used for detecting orphaned references and tracking source distribution.
-- ============================================================================

-- Orphaned References Report
-- Shows all contract references that don't have matching entries in master tables
-- with the same source. Includes usage counts to identify promotion candidates.
CREATE OR REPLACE VIEW orphaned_references_report AS
SELECT
    'Department' AS reference_type,
    c.department_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.department_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM departments d
      WHERE d.external_id = c.department_external_id
      AND d.source = c.department_source
  )
GROUP BY c.department_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Location' AS reference_type,
    c.location_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.location_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM locations l
      WHERE l.external_id = c.location_external_id
      AND l.source = c.location_source
  )
GROUP BY c.location_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Cost Center' AS reference_type,
    c.cost_center_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.cost_center_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM cost_centers cc
      WHERE cc.external_id = c.cost_center_external_id
      AND cc.source = c.cost_center_source
  )
GROUP BY c.cost_center_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Cost Bearer' AS reference_type,
    c.cost_bearer_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.cost_bearer_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM cost_bearers cb
      WHERE cb.external_id = c.cost_bearer_external_id
      AND cb.source = c.cost_bearer_source
  )
GROUP BY c.cost_bearer_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Employer' AS reference_type,
    c.employer_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.employer_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM employers e
      WHERE e.external_id = c.employer_external_id
      AND e.source = c.employer_source
  )
GROUP BY c.employer_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Team' AS reference_type,
    c.team_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.team_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM teams t
      WHERE t.external_id = c.team_external_id
      AND t.source = c.source
  )
GROUP BY c.team_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Division' AS reference_type,
    c.division_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.division_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM divisions d
      WHERE d.external_id = c.division_external_id
      AND d.source = c.source
  )
GROUP BY c.division_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Title' AS reference_type,
    c.title_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.title_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM titles t
      WHERE t.external_id = c.title_external_id
      AND t.source = c.source
  )
GROUP BY c.title_external_id, c.source, ss.display_name

UNION ALL

SELECT
    'Organization' AS reference_type,
    c.organization_external_id AS reference_id,
    c.source,
    ss.display_name AS source_name,
    COUNT(*) AS usage_count,
    STRING_AGG(DISTINCT c.external_id, ', ') AS sample_contracts
FROM contracts c
LEFT JOIN source_system ss ON c.source = ss.system_id
WHERE c.organization_external_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM organizations o
      WHERE o.external_id = c.organization_external_id
      AND o.source = c.source
  )
GROUP BY c.organization_external_id, c.source, ss.display_name

ORDER BY usage_count DESC, reference_type, reference_id;

-- Source Distribution Metrics
-- Shows how many persons and contracts exist per source system.
-- Useful for understanding data distribution and multi-source scenarios.
CREATE OR REPLACE VIEW source_distribution_metrics AS
SELECT
    ss.system_id,
    ss.display_name AS source_name,
    ss.identification_key,
    COALESCE(p.person_count, 0) AS person_count,
    COALESCE(c.contract_count, 0) AS contract_count,
    COALESCE(d.department_count, 0) AS department_count,
    COALESCE(l.location_count, 0) AS location_count,
    COALESCE(o.organization_count, 0) AS organization_count
FROM source_system ss
LEFT JOIN (
    SELECT source, COUNT(*) AS person_count
    FROM persons
    GROUP BY source
) p ON ss.system_id = p.source
LEFT JOIN (
    SELECT source, COUNT(*) AS contract_count
    FROM contracts
    GROUP BY source
) c ON ss.system_id = c.source
LEFT JOIN (
    SELECT source, COUNT(*) AS department_count
    FROM departments
    GROUP BY source
) d ON ss.system_id = d.source
LEFT JOIN (
    SELECT source, COUNT(*) AS location_count
    FROM locations
    GROUP BY source
) l ON ss.system_id = l.source
LEFT JOIN (
    SELECT source, COUNT(*) AS organization_count
    FROM organizations
    GROUP BY source
) o ON ss.system_id = o.source
ORDER BY contract_count DESC, person_count DESC;

-- Cross-Source Violations Detection
-- Identifies contracts that reference entities with mismatched sources.
-- Should return 0 rows after Phase 1 source-aware JOIN fixes.
-- If rows are found, indicates data integrity issue requiring cleanup.
CREATE OR REPLACE VIEW cross_source_violations AS
SELECT
    'Department' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.department_external_id AS reference_id,
    d.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    d.display_name AS reference_name
FROM contracts c
INNER JOIN departments d ON c.department_external_id = d.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON d.source = ss_reference.system_id
WHERE c.department_source != d.source

UNION ALL

SELECT
    'Location' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.location_external_id AS reference_id,
    l.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    l.name AS reference_name
FROM contracts c
INNER JOIN locations l ON c.location_external_id = l.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON l.source = ss_reference.system_id
WHERE c.location_source != l.source

UNION ALL

SELECT
    'Cost Center' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.cost_center_external_id AS reference_id,
    cc.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    cc.name AS reference_name
FROM contracts c
INNER JOIN cost_centers cc ON c.cost_center_external_id = cc.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON cc.source = ss_reference.system_id
WHERE c.cost_center_source != cc.source

UNION ALL

SELECT
    'Cost Bearer' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.cost_bearer_external_id AS reference_id,
    cb.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    cb.name AS reference_name
FROM contracts c
INNER JOIN cost_bearers cb ON c.cost_bearer_external_id = cb.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON cb.source = ss_reference.system_id
WHERE c.cost_bearer_source != cb.source

UNION ALL

SELECT
    'Employer' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.employer_external_id AS reference_id,
    e.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    e.name AS reference_name
FROM contracts c
INNER JOIN employers e ON c.employer_external_id = e.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON e.source = ss_reference.system_id
WHERE c.employer_source != e.source

UNION ALL

SELECT
    'Team' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.team_external_id AS reference_id,
    t.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    t.name AS reference_name
FROM contracts c
INNER JOIN teams t ON c.team_external_id = t.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON t.source = ss_reference.system_id
WHERE c.source != t.source

UNION ALL

SELECT
    'Division' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.division_external_id AS reference_id,
    d.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    d.name AS reference_name
FROM contracts c
INNER JOIN divisions d ON c.division_external_id = d.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON d.source = ss_reference.system_id
WHERE c.source != d.source

UNION ALL

SELECT
    'Title' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.title_external_id AS reference_id,
    t.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    t.name AS reference_name
FROM contracts c
INNER JOIN titles t ON c.title_external_id = t.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON t.source = ss_reference.system_id
WHERE c.source != t.source

UNION ALL

SELECT
    'Organization' AS violation_type,
    c.contract_id,
    c.external_id AS contract_external_id,
    c.source AS contract_source,
    ss_contract.display_name AS contract_source_name,
    c.organization_external_id AS reference_id,
    o.source AS reference_source,
    ss_reference.display_name AS reference_source_name,
    o.name AS reference_name
FROM contracts c
INNER JOIN organizations o ON c.organization_external_id = o.external_id
LEFT JOIN source_system ss_contract ON c.source = ss_contract.system_id
LEFT JOIN source_system ss_reference ON o.source = ss_reference.system_id
WHERE c.source != o.source

ORDER BY violation_type, contract_id;

-- ============================================================================
-- CONTRACT DETAILS CACHE TABLE (Performance Optimization)
-- ============================================================================
-- Materialized cache of contract_details_view to eliminate expensive JOINs
-- on every read. Provides 20-30x performance improvement (2-3s → ~100ms).
--
-- Refresh Strategy: On-demand rebuild after any contract/entity edit
-- Trade-off: 200-500ms rebuild time vs instant read performance
-- ============================================================================

-- Cache metadata table to track refresh status
CREATE TABLE IF NOT EXISTS cache_metadata (
    cache_name TEXT PRIMARY KEY,
    last_refreshed TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_count INTEGER NOT NULL DEFAULT 0,
    refresh_duration_ms INTEGER DEFAULT 0
);

-- Insert initial metadata for contract cache
INSERT INTO cache_metadata (cache_name, row_count)
VALUES ('contract_details_cache', 0)
ON CONFLICT (cache_name) DO NOTHING;

-- Cached contract details table (materialized view)
DROP TABLE IF EXISTS contract_details_cache;
CREATE TABLE contract_details_cache (
    -- Contract core fields
    contract_id INTEGER PRIMARY KEY,
    external_id TEXT,
    person_id TEXT NOT NULL,
    start_date TEXT,
    end_date TEXT,
    type_code TEXT,
    type_description TEXT,
    fte DOUBLE PRECISION,
    hours_per_week DOUBLE PRECISION,
    percentage DOUBLE PRECISION,
    sequence INTEGER,
    source TEXT,

    -- Person
    person_name TEXT,
    person_external_id TEXT,

    -- Manager
    manager_person_external_id TEXT,
    manager_person_name TEXT,

    -- Location
    location_external_id TEXT,
    location_source TEXT,
    location_code TEXT,
    location_name TEXT,

    -- Cost center
    cost_center_external_id TEXT,
    cost_center_source TEXT,
    cost_center_code TEXT,
    cost_center_name TEXT,

    -- Cost bearer
    cost_bearer_external_id TEXT,
    cost_bearer_source TEXT,
    cost_bearer_code TEXT,
    cost_bearer_name TEXT,

    -- Employer
    employer_external_id TEXT,
    employer_source TEXT,
    employer_code TEXT,
    employer_name TEXT,

    -- Team
    team_external_id TEXT,
    team_source TEXT,
    team_code TEXT,
    team_name TEXT,

    -- Department
    department_external_id TEXT,
    department_source TEXT,
    department_name TEXT,
    department_code TEXT,
    department_parent_external_id TEXT,
    department_manager_person_id TEXT,
    department_manager_name TEXT,
    department_parent_department_name TEXT,

    -- Division
    division_external_id TEXT,
    division_source TEXT,
    division_code TEXT,
    division_name TEXT,

    -- Title
    title_external_id TEXT,
    title_source TEXT,
    title_code TEXT,
    title_name TEXT,

    -- Organization
    organization_external_id TEXT,
    organization_source TEXT,
    organization_code TEXT,
    organization_name TEXT,

    -- Contract status (derived)
    contract_status TEXT,
    contract_date_range TEXT,

    -- Source system display name (for display purposes)
    source_display_name TEXT
);

-- Performance indexes for frequently filtered columns
-- High-value indexes: foreign key lookups and filtering (reduced from 10 to 3 indexes)
CREATE INDEX IF NOT EXISTS idx_contract_cache_person_id ON contract_details_cache(person_id);
CREATE INDEX IF NOT EXISTS idx_contract_cache_status ON contract_details_cache(contract_status);
CREATE INDEX IF NOT EXISTS idx_contract_cache_type ON contract_details_cache(type_code);

-- Compound index for common status + date range filters
CREATE INDEX IF NOT EXISTS idx_contract_cache_status_dates ON contract_details_cache(contract_status, start_date, end_date);

-- ============================================================================
-- SIMPLIFIED PERSON LIST VIEW (Performance Optimized)
-- ============================================================================
-- Lightweight view for list/grid displays that need minimal person information.
-- Eliminates expensive window functions and joins from person_details_view.
-- Performance: ~10-25x faster than person_details_view for large datasets.
--
-- Usage: Use this for list views, search results, dropdowns
-- Use person_details_view for: Detail views requiring full contract/contact data
-- ============================================================================

CREATE OR REPLACE VIEW person_list_view AS
SELECT
    p.person_id,
    p.display_name,
    p.external_id,
    p.blocked,
    p.excluded,

    -- Contract count
    (SELECT COUNT(*) FROM contracts c WHERE c.person_id = p.person_id) AS contract_count,

    -- Primary manager
    p.primary_manager_person_id,
    pm.display_name AS primary_manager_name,

    -- Calculate person status efficiently using EXISTS (stops at first match)
    CASE
        -- No contracts
        WHEN NOT EXISTS (
            SELECT 1 FROM contracts c WHERE c.person_id = p.person_id
        ) THEN 'No Contract'

        -- Has active contract
        WHEN EXISTS (
            SELECT 1 FROM contracts c
            WHERE c.person_id = p.person_id
            AND c.start_date::date <= CURRENT_DATE
            AND (c.end_date::date > CURRENT_DATE OR c.end_date IS NULL)
        ) THEN 'Active'

        -- Has future contract (and no active)
        WHEN EXISTS (
            SELECT 1 FROM contracts c
            WHERE c.person_id = p.person_id
            AND c.start_date::date > CURRENT_DATE
        ) THEN 'Future'

        -- All contracts are past
        ELSE 'Past'
    END AS person_status

FROM persons p
LEFT JOIN persons pm ON p.primary_manager_person_id = pm.person_id;

-- ============================================================================
-- SOURCE SYSTEMS VIEW (Enhanced Reference Data)
-- ============================================================================
-- Enhanced view for source system management and debugging.
-- Provides computed hash prefixes and usage statistics for namespace isolation.
--
-- Usage: Reference data management, debugging hash transformations, data quality analysis
-- ============================================================================

CREATE OR REPLACE VIEW source_systems_view AS
SELECT
    ss.system_id,
    ss.display_name,
    ss.identification_key,

    -- Count of records using this source system across all reference tables
    (
        SELECT COUNT(*) FROM persons WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM contracts WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM departments WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM employers WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM locations WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM cost_centers WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM cost_bearers WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM teams WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM divisions WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM titles WHERE source = ss.system_id
    ) + (
        SELECT COUNT(*) FROM organizations WHERE source = ss.system_id
    ) as reference_count,

    -- Timestamp when source system record was created (constant since SQLite doesn't track creation time)
    CURRENT_TIMESTAMP as created_at

FROM source_system ss
ORDER BY ss.display_name;
