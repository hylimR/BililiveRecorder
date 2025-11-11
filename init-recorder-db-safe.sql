-- BililiveRecorder Database Initialization Script (Safe Version)
-- This script safely creates database and user, handling existing objects
-- Run this as postgres superuser

-- Create user if it doesn't exist
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'recorder') THEN
    CREATE ROLE recorder WITH LOGIN PASSWORD 'NWbiBOo40XZT80Zefz15PRn/EDXUIKurzrFZqYuK1aQ=';
    RAISE NOTICE 'User "recorder" created';
  ELSE
    RAISE NOTICE 'User "recorder" already exists, skipping';
  END IF;
END
$$;

-- Create database if it doesn't exist
SELECT 'CREATE DATABASE bililiverecorder OWNER recorder'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'bililiverecorder')\gexec

-- Grant privileges on the database
GRANT ALL PRIVILEGES ON DATABASE bililiverecorder TO recorder;
