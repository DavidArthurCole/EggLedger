ALTER TABLE reports ADD COLUMN value_filter_op TEXT NOT NULL DEFAULT '';
ALTER TABLE reports ADD COLUMN value_filter_threshold REAL NOT NULL DEFAULT 0;
