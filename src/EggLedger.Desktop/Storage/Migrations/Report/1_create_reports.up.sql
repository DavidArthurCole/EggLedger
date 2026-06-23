CREATE TABLE IF NOT EXISTS reports (
    id                TEXT    PRIMARY KEY,
    account_id        TEXT    NOT NULL,
    name              TEXT    NOT NULL,
    subject           TEXT    NOT NULL,
    mode              TEXT    NOT NULL,
    display_mode      TEXT    NOT NULL,
    group_by          TEXT    NOT NULL,
    time_bucket       TEXT,
    custom_bucket_n   INTEGER,
    custom_bucket_unit TEXT,
    filters           TEXT    NOT NULL DEFAULT '{"and":[],"or":[]}',
    grid_x            INTEGER NOT NULL DEFAULT 0,
    grid_y            INTEGER NOT NULL DEFAULT 0,
    grid_w            INTEGER NOT NULL DEFAULT 1,
    grid_h            INTEGER NOT NULL DEFAULT 1,
    weight            TEXT    NOT NULL DEFAULT 'LOW',
    sort_order        INTEGER NOT NULL DEFAULT 0,
    created_at        INTEGER NOT NULL,
    updated_at        INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_reports_account ON reports(account_id);
