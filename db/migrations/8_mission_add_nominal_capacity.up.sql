-- Migration 8: Add nominal_capacity to mission table.
-- Stores the baseline capacity for the mission's (ship, duration_type, level)
-- so report queries can normalize dubcap drop counts by capacity / nominal_capacity.
-- Default 0 is the "not yet populated" sentinel; a startup backfill resolves existing rows.

ALTER TABLE mission ADD COLUMN nominal_capacity INTEGER NOT NULL DEFAULT 0;
