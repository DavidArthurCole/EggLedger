CREATE TABLE IF NOT EXISTS bot_channel_state (
    guild_id      TEXT NOT NULL,
    app_name      TEXT NOT NULL,
    kind          TEXT NOT NULL,
    discord_id    TEXT NOT NULL,
    webhook_token TEXT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (guild_id, app_name, kind)
);
