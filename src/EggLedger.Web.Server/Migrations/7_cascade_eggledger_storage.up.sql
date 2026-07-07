-- AdminEndpoints.DeleteUser / BlobEndpoints.DeleteUser only delete from users/blobs,
-- leaving orphaned rows in these six tables. Add ON DELETE CASCADE so a user delete
-- cleans up everywhere, matching sessions/blobs (migration 1). Orphans from that bug
-- predate any users row, so they're deleted first or the FK add would fail.

DELETE FROM el_mission WHERE discord_id NOT IN (SELECT discord_id FROM users);
DELETE FROM el_backup WHERE discord_id NOT IN (SELECT discord_id FROM users);
DELETE FROM el_artifact_drops WHERE discord_id NOT IN (SELECT discord_id FROM users);
DELETE FROM el_settings WHERE discord_id NOT IN (SELECT discord_id FROM users);
DELETE FROM el_reports WHERE discord_id NOT IN (SELECT discord_id FROM users);
DELETE FROM el_report_groups WHERE discord_id NOT IN (SELECT discord_id FROM users);

-- A killed/restarted migration attempt can have already committed some of these ADD
-- CONSTRAINT statements before dying, so each is guarded to make re-running the whole
-- file after a crash safe instead of erroring on "constraint already exists".
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_mission_discord_id') THEN
        ALTER TABLE el_mission
            ADD CONSTRAINT fk_el_mission_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_backup_discord_id') THEN
        ALTER TABLE el_backup
            ADD CONSTRAINT fk_el_backup_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_artifact_drops_discord_id') THEN
        ALTER TABLE el_artifact_drops
            ADD CONSTRAINT fk_el_artifact_drops_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_settings_discord_id') THEN
        ALTER TABLE el_settings
            ADD CONSTRAINT fk_el_settings_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_reports_discord_id') THEN
        ALTER TABLE el_reports
            ADD CONSTRAINT fk_el_reports_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_el_report_groups_discord_id') THEN
        ALTER TABLE el_report_groups
            ADD CONSTRAINT fk_el_report_groups_discord_id FOREIGN KEY (discord_id) REFERENCES users(discord_id) ON DELETE CASCADE;
    END IF;
END $$;
