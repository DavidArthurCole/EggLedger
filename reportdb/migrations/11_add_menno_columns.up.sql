ALTER TABLE reports ADD COLUMN menno_enabled BOOLEAN NOT NULL DEFAULT 0;
ALTER TABLE reports ADD COLUMN menno_compare_mode TEXT NOT NULL DEFAULT 'side_by_side';
