-- Migration 6: Add indexed filter columns to the mission table so that common
-- filter queries (ship, duration, level, target, dubcap, etc.) can be satisfied
-- without decompressing the payload blob.
--
-- All new integer columns default to -1 as a "not yet populated" sentinel.
-- Boolean columns (is_dub_cap, is_bugged_cap) default to 0 (false).
-- return_timestamp defaults to 0.
-- A background backfill pass at startup will resolve rows where ship = -1.

ALTER TABLE mission ADD COLUMN ship             INTEGER NOT NULL DEFAULT -1;
ALTER TABLE mission ADD COLUMN duration_type    INTEGER NOT NULL DEFAULT -1;
ALTER TABLE mission ADD COLUMN level            INTEGER NOT NULL DEFAULT -1;
ALTER TABLE mission ADD COLUMN capacity         INTEGER NOT NULL DEFAULT -1;
ALTER TABLE mission ADD COLUMN is_dub_cap       INTEGER NOT NULL DEFAULT 0;
ALTER TABLE mission ADD COLUMN is_bugged_cap    INTEGER NOT NULL DEFAULT 0;
ALTER TABLE mission ADD COLUMN target           INTEGER NOT NULL DEFAULT -1;
ALTER TABLE mission ADD COLUMN return_timestamp REAL    NOT NULL DEFAULT 0;

CREATE INDEX idx_mission_player_ship      ON mission(player_id, ship);
CREATE INDEX idx_mission_player_duration  ON mission(player_id, duration_type);
CREATE INDEX idx_mission_player_level     ON mission(player_id, level);
CREATE INDEX idx_mission_player_dubcap    ON mission(player_id, is_dub_cap);
CREATE INDEX idx_mission_player_buggedcap ON mission(player_id, is_bugged_cap);
CREATE INDEX idx_mission_player_target    ON mission(player_id, target);
CREATE INDEX idx_mission_player_ts        ON mission(player_id, start_timestamp);
CREATE INDEX idx_mission_player_return    ON mission(player_id, return_timestamp);
CREATE INDEX idx_mission_player_type      ON mission(player_id, mission_type);
