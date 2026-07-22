CREATE TABLE IF NOT EXISTS deploy_state (
    app_name TEXT PRIMARY KEY,
    git_sha TEXT NOT NULL,
    semver TEXT NOT NULL,
    notified_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
