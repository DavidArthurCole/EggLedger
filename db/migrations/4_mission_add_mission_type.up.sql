-- During the Virtue update, MissionType `type` was added to MissionInfo.
-- Because this needs to be queryable to be fast, we need to add it to the database directly.
-- Any ship that exists in the database from before this update gets set to 0 (Standard).
-- Otherwise default to -1, and let the app set it on first fetch.

ALTER TABLE mission_info ADD COLUMN mission_type INTEGER NOT NULL DEFAULT -1;
-- Leaving a window of ~12 hours before the update as the cutoff, 
-- Wednesday, September 17, 2025 12:00:00 AM, --> 1758067200
UPDATE mission_info SET mission_type = 0 WHERE start_timestamp < 1758067200;