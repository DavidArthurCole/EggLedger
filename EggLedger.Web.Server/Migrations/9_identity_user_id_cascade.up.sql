-- SyncKit.Identity is now the source of truth for user_id, minted independently of EggLedger's
-- own local users table. On a returning user's first post-cutover login, identity's resolved
-- user_id differs from the local row's existing user_id for the same discord_id; StorePending
-- repoints the local row to identity's user_id, which needs ON UPDATE CASCADE so sessions/blobs
-- (the only two local tables with an FK to users) move automatically.

ALTER TABLE sessions DROP CONSTRAINT IF EXISTS sessions_user_id_fkey;
ALTER TABLE sessions ADD CONSTRAINT sessions_user_id_fkey
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE blobs DROP CONSTRAINT IF EXISTS blobs_user_id_fkey;
ALTER TABLE blobs ADD CONSTRAINT blobs_user_id_fkey
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE ON UPDATE CASCADE;
