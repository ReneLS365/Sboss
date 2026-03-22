CREATE TABLE IF NOT EXISTS contract_jobs (
  contract_job_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  owning_account_id UUID NOT NULL REFERENCES accounts(account_id),
  current_state TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  CONSTRAINT contract_jobs_current_state_valid CHECK (current_state IN ('Draft', 'Open', 'Accepted', 'InProgress', 'Completed', 'Failed', 'Cancelled'))
);

CREATE TABLE IF NOT EXISTS contract_job_transitions (
  contract_job_transition_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  contract_job_id UUID NOT NULL REFERENCES contract_jobs(contract_job_id) ON DELETE CASCADE,
  idempotency_key TEXT NOT NULL,
  from_state TEXT NOT NULL,
  to_state TEXT NOT NULL,
  resulting_version BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT contract_job_transitions_idempotency_unique UNIQUE (contract_job_id, idempotency_key),
  CONSTRAINT contract_job_transitions_from_state_valid CHECK (from_state IN ('Draft', 'Open', 'Accepted', 'InProgress', 'Completed', 'Failed', 'Cancelled')),
  CONSTRAINT contract_job_transitions_to_state_valid CHECK (to_state IN ('Draft', 'Open', 'Accepted', 'InProgress', 'Completed', 'Failed', 'Cancelled')),
  CONSTRAINT contract_job_transitions_resulting_version_positive CHECK (resulting_version > 0)
);
