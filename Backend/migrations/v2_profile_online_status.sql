-- Migration v2: Profile & Online Status
-- Run once against hermes_db

USE hermes_db;

-- Add Username to users table (unique login handle)
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS Username VARCHAR(50) UNIQUE DEFAULT NULL;

-- Add Bio, IsOnline, CreatedAt, LastSeenAt to userinfo
ALTER TABLE userinfo
    ADD COLUMN IF NOT EXISTS Bio VARCHAR(255) DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS IsOnline BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ADD COLUMN IF NOT EXISTS LastSeenAt DATETIME NULL;

-- Backfill participants table with LastSeenMessageId column if missing (older schema)
ALTER TABLE participants
    ADD COLUMN IF NOT EXISTS LastSeenMessageId BIGINT NOT NULL DEFAULT 0;
