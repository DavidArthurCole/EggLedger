CREATE TABLE IF NOT EXISTS backup (
    player_id TEXT PRIMARY KEY,
    recorded_at REAL NOT NULL,
    payload BLOB NOT NULL
);
CREATE TABLE IF NOT EXISTS mission (
    player_id TEXT NOT NULL,
    mission_id TEXT NOT NULL,
    start_timestamp REAL NOT NULL,
    complete_payload BLOB NOT NULL,
    PRIMARY KEY (player_id, mission_id)
);
