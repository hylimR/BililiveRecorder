-- BililiveRecorder Schema Setup
-- Connect to bililiverecorder database first: \c bililiverecorder
-- Run this as postgres superuser or recorder user

-- Create schema for recorder
CREATE SCHEMA IF NOT EXISTS recorder;

-- Grant ownership to recorder user
ALTER SCHEMA recorder OWNER TO recorder;

-- Grant all privileges on the schema
GRANT ALL ON SCHEMA recorder TO recorder;

-- Set default privileges for future tables
ALTER DEFAULT PRIVILEGES IN SCHEMA recorder GRANT ALL ON TABLES TO recorder;
ALTER DEFAULT PRIVILEGES IN SCHEMA recorder GRANT ALL ON SEQUENCES TO recorder;

-- Allow recorder user to create tables in public schema (fallback)
GRANT ALL ON SCHEMA public TO recorder;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO recorder;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO recorder;

-- Note: The BililiveRecorder application will automatically create the 'configs' table
-- when it first runs with PostgreSQL persistence enabled
