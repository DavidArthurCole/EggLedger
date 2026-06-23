-- Backup payload used to be response payload from /ei/first_contact which used
-- an authenticated message. The endpoint was shut down and replaced with
-- /ei/bot_first_contact which uses an unauthenticated message.

ALTER TABLE backup ADD COLUMN payload_authenticated INTEGER NOT NULL DEFAULT FALSE;
-- All existing backups recorded before this version were authenticated.
UPDATE backup SET payload_authenticated = TRUE;
