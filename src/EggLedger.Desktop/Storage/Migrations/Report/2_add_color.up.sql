-- Migration 2: user-configurable chart bar color per report.
ALTER TABLE reports ADD COLUMN color TEXT NOT NULL DEFAULT '#6366f1';
