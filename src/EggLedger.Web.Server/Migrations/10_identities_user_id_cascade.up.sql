-- Migration 9 added ON UPDATE CASCADE to sessions/blobs for the users.user_id repoint in
-- StorePending, but missed identities (added in migration 8), which also FKs users(user_id).
-- A returning user whose local user_id needs repointing then hits a bare FK violation on
-- identities and login fails outright.

ALTER TABLE identities DROP CONSTRAINT IF EXISTS identities_user_id_fkey;
ALTER TABLE identities ADD CONSTRAINT identities_user_id_fkey
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE ON UPDATE CASCADE;
