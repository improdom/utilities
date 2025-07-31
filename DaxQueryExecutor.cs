-- Enable pgcrypto extension (Azure supported)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =======================================
-- 1. Table: worker_model
-- =======================================
CREATE TABLE worker_model (
    worker_model_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_name TEXT NOT NULL,
    workspace_id UUID NOT NULL,
    dataset_id UUID NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =======================================
-- 2. Table: model_state (singleton)
-- =======================================
CREATE TABLE model_state (
    model_state_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reader_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    writer_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    status TEXT NOT NULL DEFAULT 'idle',
    last_switch_time TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Enforce singleton
CREATE UNIQUE INDEX one_model_state ON model_state ((true));

-- =======================================
-- 3. Table: model_role_history
-- =======================================
CREATE TABLE model_role_history (
    model_role_history_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    worker_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    role TEXT NOT NULL CHECK (role IN ('reader', 'writer')),
    is_current BOOLEAN NOT NULL DEFAULT FALSE,
    activated_at TIMESTAMPTZ NOT NULL,
    deactivated_at TIMESTAMPTZ,
    switch_reason TEXT
);

-- =======================================
-- 4. Table: model_refresh_log
-- =======================================
CREATE TABLE model_refresh_log (
    model_refresh_log_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    worker_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    refresh_started_at TIMESTAMPTZ NOT NULL,
    refresh_ended_at TIMESTAMPTZ,
    status TEXT NOT NULL CHECK (status IN ('in_progress', 'success', 'failure')),
    message TEXT
);

-- Optional indexes
CREATE INDEX idx_model_role_current ON model_role_history(is_current) WHERE is_current = true;
CREATE INDEX idx_model_refresh_worker_status ON model_refresh_log(worker_model_id, status);
