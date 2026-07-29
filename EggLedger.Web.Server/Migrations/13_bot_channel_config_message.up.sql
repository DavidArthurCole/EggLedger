ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS dashboard_embed_json TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS success_message_json TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS failure_message_json TEXT;
ALTER TABLE bot_channel_config ADD COLUMN IF NOT EXISTS uptodate_message_json TEXT;
