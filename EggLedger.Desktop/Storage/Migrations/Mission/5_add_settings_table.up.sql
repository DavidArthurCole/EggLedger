-- The settings table replaces internal/storage.json for persisting app preferences.
-- Each row is a single key-value pair where the key must be unique (PRIMARY KEY).
-- DEFAULT '' is a safety net for any inserts that omit the value field.

CREATE TABLE IF NOT EXISTS settings (
    key TEXT NOT NULL PRIMARY KEY,
    value TEXT NOT NULL DEFAULT ''
);
