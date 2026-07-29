-- 1_init_schema.up.sql defined player_id as TEXT PRIMARY KEY. That was a brain
-- fart since I obviously wanted to store multiple backups per player; that's
-- why there's a recorded_at.
--
-- However, I already shipped out a beta to private testers with the previous
-- schema, so we have to correct this error in a migration.
--
-- While we're at it, we change recorded_at to backed_up_at, since I decided to
-- store the timestamp of the backup instead of the timestamp when the backup is
-- fetched.

CREATE TABLE backup_tmp (
    id INTEGER PRIMARY KEY,
    player_id TEXT NOT NULL,
    backed_up_at REAL NOT NULL,
    payload BLOB NOT NULL
);
INSERT INTO backup_tmp (player_id, backed_up_at, payload)
    SELECT player_id, recorded_at, payload
    FROM backup;
DROP TABLE backup;
ALTER TABLE backup_tmp RENAME TO backup;
