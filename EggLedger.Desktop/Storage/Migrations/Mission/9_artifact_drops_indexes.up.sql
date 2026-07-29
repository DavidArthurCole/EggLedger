-- Indexes supporting report queries over artifact_drops:
--   * GROUP BY artifact name (artifact_id) and tier (level)
--   * the "Drops contains / does not contain" EXISTS subquery, which matches on
--     (mission_id, player_id, artifact_id)
-- Migration 7 only added (player_id, rarity) and (player_id, spec_type), so
-- artifact_id and level lookups previously scanned more rows than necessary.
CREATE INDEX IF NOT EXISTS idx_artifact_drops_player_artifact
    ON artifact_drops(player_id, artifact_id);

CREATE INDEX IF NOT EXISTS idx_artifact_drops_player_level
    ON artifact_drops(player_id, level);
