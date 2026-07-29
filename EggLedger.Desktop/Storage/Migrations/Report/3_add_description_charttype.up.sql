-- Migration 3: user-configurable description and chart type per report.
ALTER TABLE reports ADD COLUMN description TEXT NOT NULL DEFAULT '';
ALTER TABLE reports ADD COLUMN chart_type TEXT NOT NULL DEFAULT 'bar';
