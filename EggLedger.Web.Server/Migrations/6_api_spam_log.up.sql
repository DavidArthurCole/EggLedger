-- Per-client record of invalid /api/v1 requests (no route matched -> 404), for the
-- admin "potentially identified spam" view. One row per distinct (ip, method, path);
-- hits incremented on repeat so the table is bounded by distinct scanners, not volume.
-- Valid requests are still count-only via the in-process ApiMetrics ring.

CREATE TABLE IF NOT EXISTS el_api_spam (
    ip         TEXT   NOT NULL,
    method     TEXT   NOT NULL,
    path       TEXT   NOT NULL,
    user_agent TEXT   NOT NULL DEFAULT '',
    first_seen BIGINT NOT NULL,
    last_seen  BIGINT NOT NULL,
    hits       BIGINT NOT NULL DEFAULT 1,
    PRIMARY KEY (ip, method, path)
);
CREATE INDEX IF NOT EXISTS idx_el_api_spam_last ON el_api_spam(last_seen DESC);
