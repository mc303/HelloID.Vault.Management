-- ============================================================================
-- Supabase RLS Configuration for HelloID Vault Connector
-- ============================================================================
-- This script configures Row Level Security (RLS) for the HelloID connector
-- to work with authenticated users.
--
-- What this script does:
-- 1. Grants table permissions to authenticated and anon roles
-- 2. Enables RLS on all tables in public schema
-- 3. Creates policies for anon (read-only) and authenticated (full access)
--
-- Run this in Supabase SQL Editor after creating your database schema.
-- ============================================================================

-- ============================================================================
-- STEP 1: Grant Table Permissions
-- ============================================================================
-- RLS policies only work if the role has table-level permissions.
-- This grants SELECT, INSERT, UPDATE, DELETE on all tables.

GRANT ALL ON ALL TABLES IN SCHEMA public TO authenticated;
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon;

-- Grant usage on sequences (for auto-increment columns)
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO authenticated;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO anon;

-- Grant usage on schema
GRANT USAGE ON SCHEMA public TO authenticated;
GRANT USAGE ON SCHEMA public TO anon;

-- ============================================================================
-- STEP 2: Enable RLS and Create Policies
-- ============================================================================
-- Uses a dynamic loop to apply to all tables in public schema

DO $$ 
DECLARE 
    r RECORD;
BEGIN 
    -- Loop through all tables in 'public' schema
    FOR r IN (
        SELECT table_schema, table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_type = 'BASE TABLE'
    ) 
    LOOP 
        -- Enable RLS on table
        EXECUTE format(
            'ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY;', 
            r.table_schema, r.table_name
        );
        
        -- Policy 1: Anon (unauthenticated) - Read-only access
        EXECUTE format(
            'DROP POLICY IF EXISTS "api_anon_read" ON %I.%I;', 
            r.table_schema, r.table_name
        );
        EXECUTE format(
            'CREATE POLICY "api_anon_read" ON %I.%I FOR SELECT TO anon USING (true);', 
            r.table_schema, r.table_name
        );

        -- Policy 2: Authenticated - Full access (read, insert, update, delete)
        EXECUTE format(
            'DROP POLICY IF EXISTS "api_auth_full" ON %I.%I;', 
            r.table_schema, r.table_name
        );
        EXECUTE format(
            'CREATE POLICY "api_auth_full" ON %I.%I FOR ALL TO authenticated USING (true) WITH CHECK (true);', 
            r.table_schema, r.table_name
        );
        
        RAISE NOTICE 'RLS configured for: %.%', r.table_schema, r.table_name;
    END LOOP; 
END $$;

-- ============================================================================
-- STEP 3: Verify Configuration (Optional)
-- ============================================================================
-- Run these queries to verify the setup is correct

-- Check table permissions
-- SELECT grantee, table_name, privilege_type 
-- FROM information_schema.table_privileges 
-- WHERE table_schema = 'public' 
-- AND grantee IN ('authenticated', 'anon')
-- ORDER BY table_name, grantee;

-- Check RLS policies
-- SELECT schemaname, tablename, policyname, cmd, roles 
-- FROM pg_policies 
-- WHERE schemaname = 'public' 
-- ORDER BY tablename, policyname;

-- ============================================================================
-- NOTES
-- ============================================================================
-- 
-- Service Account Setup:
-- 1. Go to Supabase Dashboard → Authentication → Users
-- 2. Create a user (e.g., helloid-connector@yourdomain.com)
-- 3. Use this email/password in the HelloID connector configuration
--
-- Security:
-- - The "anon" role can only READ data (for public APIs)
-- - The "authenticated" role has full access (for logged-in users)
-- - HelloID authenticates as the service account user
-- - All operations are logged and auditable
--
-- Troubleshooting:
-- - If you get "permission denied", ensure STEP 1 GRANT commands ran
-- - If you get "policy violation", check the policies exist (STEP 2)
-- - Use the verify queries above to diagnose issues
-- ============================================================================
