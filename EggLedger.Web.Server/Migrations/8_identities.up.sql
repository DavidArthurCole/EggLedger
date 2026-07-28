-- Provider-neutral identity: user_id UUID becomes the real primary key for `users`,
-- with `identities` linking (provider, subject) pairs (e.g. discord/<snowflake>,
-- authentik/<sub>) to it so one account can carry both. Backfills user_id for every
-- table previously partitioned by discord_id, then repoints the users PK and the
-- sessions/blobs FKs from discord_id to user_id. discord_id becomes nullable so an
-- Authentik-only signup with no linked Discord account doesn't collide on empty string.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER TABLE users ADD COLUMN IF NOT EXISTS user_id UUID NOT NULL DEFAULT gen_random_uuid();
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_user_id ON users(user_id);

CREATE TABLE IF NOT EXISTS identities (
    user_id    UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    provider   TEXT NOT NULL,
    subject    TEXT NOT NULL,
    linked_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (provider, subject)
);
CREATE INDEX IF NOT EXISTS idx_identities_user ON identities(user_id);

INSERT INTO identities (user_id, provider, subject)
SELECT user_id, 'discord', discord_id FROM users
WHERE discord_id IS NOT NULL AND discord_id <> ''
ON CONFLICT (provider, subject) DO NOTHING;

ALTER TABLE sessions ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE sessions SET user_id = u.user_id FROM users u WHERE sessions.discord_id = u.discord_id AND sessions.user_id IS NULL;
ALTER TABLE sessions ALTER COLUMN user_id SET NOT NULL;
CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id);

ALTER TABLE blobs ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE blobs SET user_id = u.user_id FROM users u WHERE blobs.discord_id = u.discord_id AND blobs.user_id IS NULL;
ALTER TABLE blobs ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_blobs_user_name ON blobs(user_id, name);

ALTER TABLE el_mission ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_mission SET user_id = u.user_id FROM users u WHERE el_mission.discord_id = u.discord_id AND el_mission.user_id IS NULL;
ALTER TABLE el_mission ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_mission_user_player_mission ON el_mission(user_id, player_id, mission_id);
CREATE INDEX IF NOT EXISTS idx_el_mission_user_player      ON el_mission(user_id, player_id);
CREATE INDEX IF NOT EXISTS idx_el_mission_user_player_ship ON el_mission(user_id, player_id, ship);
CREATE INDEX IF NOT EXISTS idx_el_mission_user_player_ts   ON el_mission(user_id, player_id, start_timestamp);

ALTER TABLE el_backup ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_backup SET user_id = u.user_id FROM users u WHERE el_backup.discord_id = u.discord_id AND el_backup.user_id IS NULL;
ALTER TABLE el_backup ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_backup_user_player ON el_backup(user_id, player_id);

ALTER TABLE el_artifact_drops ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_artifact_drops SET user_id = u.user_id FROM users u WHERE el_artifact_drops.discord_id = u.discord_id AND el_artifact_drops.user_id IS NULL;
ALTER TABLE el_artifact_drops ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_drops_user_mission_player_dropidx ON el_artifact_drops(user_id, mission_id, player_id, drop_index);
CREATE INDEX IF NOT EXISTS idx_el_drops_user_player          ON el_artifact_drops(user_id, player_id);
CREATE INDEX IF NOT EXISTS idx_el_drops_user_player_rarity   ON el_artifact_drops(user_id, player_id, rarity);
CREATE INDEX IF NOT EXISTS idx_el_drops_user_player_artifact ON el_artifact_drops(user_id, player_id, artifact_id);
CREATE INDEX IF NOT EXISTS idx_el_drops_user_player_level    ON el_artifact_drops(user_id, player_id, level);

ALTER TABLE el_settings ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_settings SET user_id = u.user_id FROM users u WHERE el_settings.discord_id = u.discord_id AND el_settings.user_id IS NULL;
ALTER TABLE el_settings ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_settings_user_key ON el_settings(user_id, key);

ALTER TABLE el_reports ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_reports SET user_id = u.user_id FROM users u WHERE el_reports.discord_id = u.discord_id AND el_reports.user_id IS NULL;
ALTER TABLE el_reports ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_reports_user_id ON el_reports(user_id, id);
CREATE INDEX IF NOT EXISTS idx_el_reports_user_account ON el_reports(user_id, account_id);

ALTER TABLE el_report_groups ADD COLUMN IF NOT EXISTS user_id UUID;
UPDATE el_report_groups SET user_id = u.user_id FROM users u WHERE el_report_groups.discord_id = u.discord_id AND el_report_groups.user_id IS NULL;
ALTER TABLE el_report_groups ALTER COLUMN user_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_el_report_groups_user_id ON el_report_groups(user_id, id);

-- Drop the discord_id-referencing FKs before repointing the users PK: Postgres refuses to
-- drop users_pkey while sessions/blobs/el_* still reference it via their discord_id FK
-- (sessions/blobs from migration 1, the six el_* FKs from migration 7).
ALTER TABLE sessions DROP CONSTRAINT IF EXISTS sessions_discord_id_fkey;
ALTER TABLE blobs DROP CONSTRAINT IF EXISTS blobs_discord_id_fkey;
ALTER TABLE el_mission DROP CONSTRAINT IF EXISTS fk_el_mission_discord_id;
ALTER TABLE el_backup DROP CONSTRAINT IF EXISTS fk_el_backup_discord_id;
ALTER TABLE el_artifact_drops DROP CONSTRAINT IF EXISTS fk_el_artifact_drops_discord_id;
ALTER TABLE el_settings DROP CONSTRAINT IF EXISTS fk_el_settings_discord_id;
ALTER TABLE el_reports DROP CONSTRAINT IF EXISTS fk_el_reports_discord_id;
ALTER TABLE el_report_groups DROP CONSTRAINT IF EXISTS fk_el_report_groups_discord_id;

ALTER TABLE users DROP CONSTRAINT IF EXISTS users_pkey;
ALTER TABLE users ADD PRIMARY KEY (user_id);
ALTER TABLE users ALTER COLUMN discord_id DROP NOT NULL;
ALTER TABLE users ALTER COLUMN discord_id DROP DEFAULT;
UPDATE users SET discord_id = NULL WHERE discord_id = '';
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_discord_id ON users(discord_id) WHERE discord_id IS NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'sessions_user_id_fkey') THEN
        ALTER TABLE sessions ADD CONSTRAINT sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'blobs_user_id_fkey') THEN
        ALTER TABLE blobs ADD CONSTRAINT blobs_user_id_fkey FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE;
    END IF;
END $$;

-- Every write path (PostgresIndexedDb, AuthEndpoints, BlobEndpoints) now inserts user_id only
-- and never sets discord_id, so the old discord_id NOT NULL constraints and discord_id-keyed
-- primary keys must be relaxed/dropped here or every insert fails. sessions' PK is `token`
-- (unaffected); blobs and every el_* table had a discord_id-inclusive composite PK that must
-- be replaced by the user_id-based unique index created above.
ALTER TABLE sessions ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE blobs DROP CONSTRAINT IF EXISTS blobs_pkey;
ALTER TABLE blobs ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE el_mission DROP CONSTRAINT IF EXISTS el_mission_pkey;
ALTER TABLE el_mission ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE el_backup DROP CONSTRAINT IF EXISTS el_backup_pkey;
ALTER TABLE el_backup ALTER COLUMN discord_id DROP NOT NULL;

-- The old UNIQUE(discord_id, mission_id, player_id, drop_index) constraint's auto-generated
-- name exceeds Postgres' 63-byte identifier limit and gets silently truncated, so its exact
-- name can't be hardcoded reliably; look it up by the columns it actually covers instead.
DO $$
DECLARE
    old_unique_name TEXT;
BEGIN
    SELECT con.conname INTO old_unique_name
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    WHERE rel.relname = 'el_artifact_drops'
      AND con.contype = 'u'
      AND con.conkey = (
          SELECT array_agg(attnum ORDER BY ord)
          FROM unnest(ARRAY['discord_id', 'mission_id', 'player_id', 'drop_index']) WITH ORDINALITY AS cols(attname, ord)
          JOIN pg_attribute attr ON attr.attrelid = rel.oid AND attr.attname = cols.attname
      );
    IF old_unique_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE el_artifact_drops DROP CONSTRAINT %I', old_unique_name);
    END IF;
END $$;
ALTER TABLE el_artifact_drops ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE el_settings DROP CONSTRAINT IF EXISTS el_settings_pkey;
ALTER TABLE el_settings ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE el_reports DROP CONSTRAINT IF EXISTS el_reports_pkey;
ALTER TABLE el_reports ALTER COLUMN discord_id DROP NOT NULL;

ALTER TABLE el_report_groups DROP CONSTRAINT IF EXISTS el_report_groups_pkey;
ALTER TABLE el_report_groups ALTER COLUMN discord_id DROP NOT NULL;

-- ADD PRIMARY KEY USING INDEX has no IF NOT EXISTS form, so each is guarded against a
-- prior partial run having already promoted the index to a PK.
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'blobs'::regclass AND contype = 'p') THEN
        ALTER TABLE blobs ADD PRIMARY KEY USING INDEX idx_blobs_user_name;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'el_mission'::regclass AND contype = 'p') THEN
        ALTER TABLE el_mission ADD PRIMARY KEY USING INDEX idx_el_mission_user_player_mission;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'el_backup'::regclass AND contype = 'p') THEN
        ALTER TABLE el_backup ADD PRIMARY KEY USING INDEX idx_el_backup_user_player;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'el_settings'::regclass AND contype = 'p') THEN
        ALTER TABLE el_settings ADD PRIMARY KEY USING INDEX idx_el_settings_user_key;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'el_reports'::regclass AND contype = 'p') THEN
        ALTER TABLE el_reports ADD PRIMARY KEY USING INDEX idx_el_reports_user_id;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'el_report_groups'::regclass AND contype = 'p') THEN
        ALTER TABLE el_report_groups ADD PRIMARY KEY USING INDEX idx_el_report_groups_user_id;
    END IF;
END $$;
