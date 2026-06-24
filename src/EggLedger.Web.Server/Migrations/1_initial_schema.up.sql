CREATE TABLE IF NOT EXISTS users (
    discord_id TEXT PRIMARY KEY,
    created_at BIGINT NOT NULL
);

CREATE TABLE IF NOT EXISTS sessions (
    token TEXT PRIMARY KEY,
    discord_id TEXT NOT NULL REFERENCES users(discord_id) ON DELETE CASCADE,
    expires_at BIGINT NOT NULL
);

CREATE TABLE IF NOT EXISTS blobs (
    discord_id TEXT NOT NULL REFERENCES users(discord_id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    ciphertext TEXT NOT NULL,
    updated_at BIGINT NOT NULL,
    PRIMARY KEY (discord_id, name)
);

CREATE TABLE IF NOT EXISTS pending_auth (
    state TEXT PRIMARY KEY,
    session_token TEXT,
    expires_at BIGINT NOT NULL
);
