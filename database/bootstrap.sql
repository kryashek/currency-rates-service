-- Run only through Bootstrap.ps1 against a dedicated Currency Rates database.
-- Administrative credentials are used here, never by DbMigrator or APIs.
\getenv identity_password CRS_IDENTITY_PASSWORD
\getenv finance_password CRS_FINANCE_PASSWORD
\getenv migrator_password CRS_MIGRATOR_PASSWORD

DO $guard$
BEGIN
    IF current_setting('server_version_num')::int NOT BETWEEN 160000 AND 169999 THEN
        RAISE EXCEPTION 'This bootstrap targets PostgreSQL 16';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname IN
            ('rates_owner','identity_runtime','finance_runtime','rates_migrator')
        AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls
             OR rolcanlogin <> (rolname <> 'rates_owner'))
    ) OR EXISTS (
        SELECT 1 FROM pg_auth_members WHERE member IN (
            SELECT oid FROM pg_roles WHERE rolname IN
                ('rates_owner','identity_runtime','finance_runtime','rates_migrator'))
    ) THEN
        RAISE EXCEPTION 'Existing managed role attributes or memberships are unsafe';
    END IF;
END
$guard$;

BEGIN;
SELECT 'CREATE ROLE rates_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rates_owner')
\gexec
SELECT format('CREATE ROLE identity_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', :'identity_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'identity_runtime')
\gexec
SELECT format('CREATE ROLE finance_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', :'finance_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'finance_runtime')
\gexec
SELECT format('CREATE ROLE rates_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', :'migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rates_migrator')
\gexec
COMMIT;

-- CREATE DATABASE cannot run in a transaction. The next steps are rerunnable.
SELECT format('CREATE DATABASE %I OWNER rates_owner ENCODING %L TEMPLATE template0', :'db_name', 'UTF8')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db_name')
\gexec
SELECT datdba = 'rates_owner'::regrole AS safe_owner
FROM pg_database WHERE datname = :'db_name'
\gset
\if :safe_owner
\else
    SELECT 1 / 0; -- ON_ERROR_STOP: refuse a database owned by another application
\endif

\connect :db_name
BEGIN;
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname IN ('identity','finance')
               AND nspowner <> 'rates_migrator'::regrole) THEN
        RAISE EXCEPTION 'Existing application schema has an unexpected owner';
    END IF;
END
$guard$;
REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC, identity_runtime, finance_runtime, rates_migrator;
GRANT CONNECT ON DATABASE :"db_name" TO identity_runtime, finance_runtime, rates_migrator;
REVOKE ALL ON SCHEMA public FROM PUBLIC, identity_runtime, finance_runtime, rates_migrator;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION rates_migrator;
CREATE SCHEMA IF NOT EXISTS finance AUTHORIZATION rates_migrator;
REVOKE ALL ON SCHEMA identity FROM PUBLIC, identity_runtime, finance_runtime;
REVOKE ALL ON SCHEMA finance FROM PUBLIC, identity_runtime, finance_runtime;
GRANT USAGE ON SCHEMA identity TO identity_runtime;
GRANT USAGE ON SCHEMA finance TO finance_runtime;
REVOKE ALL ON ALL TABLES IN SCHEMA identity FROM PUBLIC, finance_runtime;
REVOKE ALL ON ALL TABLES IN SCHEMA finance FROM PUBLIC, identity_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE rates_migrator REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE rates_migrator REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER ROLE identity_runtime IN DATABASE :"db_name" SET search_path = pg_catalog, identity;
ALTER ROLE finance_runtime IN DATABASE :"db_name" SET search_path = pg_catalog, finance;
ALTER ROLE rates_migrator IN DATABASE :"db_name" SET search_path = pg_catalog;
COMMIT;
