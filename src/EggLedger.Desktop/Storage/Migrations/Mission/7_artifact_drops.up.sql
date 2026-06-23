-- Migration 7: one row per artifact drop per mission for the Reports query engine.
-- Enables SQL GROUP BY on rarity, spec_type, artifact_id, etc. without decompressing
-- protobuf blobs. Populated at fetch time (fetchCompleteMissionWithContext) and
-- backfilled at startup for pre-existing missions (backfill.go: BackfillArtifactDrops).
-- drop_index is the 0-based position in CompleteMissionResponse.Artifacts; the UNIQUE
-- constraint on (mission_id, player_id, drop_index) provides DB-level idempotency so
-- backfill re-runs are safe without application-layer COUNT checks.
CREATE TABLE IF NOT EXISTS artifact_drops (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    mission_id  TEXT    NOT NULL,
    player_id   TEXT    NOT NULL,
    drop_index  INTEGER NOT NULL,
    artifact_id INTEGER NOT NULL,
    spec_type   TEXT    NOT NULL,
    level       INTEGER NOT NULL,
    rarity      INTEGER NOT NULL,
    quality     REAL    NOT NULL DEFAULT 0,
    UNIQUE(mission_id, player_id, drop_index)
);

CREATE INDEX IF NOT EXISTS idx_artifact_drops_player_rarity
    ON artifact_drops(player_id, rarity);
CREATE INDEX IF NOT EXISTS idx_artifact_drops_player_spec
    ON artifact_drops(player_id, spec_type);
