CREATE TABLE IF NOT EXISTS bot_channel_config (
    guild_id TEXT NOT NULL,
    app_name TEXT NOT NULL,
    dashboard_channel_id TEXT,
    enabled_threads TEXT,
    success_template TEXT,
    failure_template TEXT,
    already_up_to_date_template TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (guild_id, app_name)
);

ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS github_feed_thread_id TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS deploy_notifications_thread_id TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS success_embed_json TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS failure_embed_json TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS uptodate_embed_json TEXT;
