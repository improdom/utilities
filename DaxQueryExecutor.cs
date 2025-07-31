-- Enable UUID support
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =======================================
-- 1. Table: worker_model
-- =======================================
CREATE TABLE worker_model (
    worker_model_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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
    model_state_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reader_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    writer_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    status TEXT NOT NULL DEFAULT 'idle',
    last_switch_time TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Enforce only one row in model_state
CREATE UNIQUE INDEX one_model_state ON model_state ((true));

-- =======================================
-- 3. Table: model_role_history
-- =======================================
CREATE TABLE model_role_history (
    model_role_history_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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
    model_refresh_log_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    worker_model_id UUID NOT NULL REFERENCES worker_model(worker_model_id),
    refresh_started_at TIMESTAMPTZ NOT NULL,
    refresh_ended_at TIMESTAMPTZ,
    status TEXT NOT NULL CHECK (status IN ('in_progress', 'success', 'failure')),
    message TEXT
);

-- =======================================
-- Indexes for faster lookups (optional)
-- =======================================
CREATE INDEX idx_model_role_current ON model_role_history(is_current) WHERE is_current = true;
CREATE INDEX idx_model_refresh_worker_status ON model_refresh_log(worker_model_id, status);
