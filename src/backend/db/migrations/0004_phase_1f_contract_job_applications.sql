CREATE TABLE IF NOT EXISTS contract_job_applications (
  contract_job_application_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  contract_job_id UUID NOT NULL REFERENCES contract_jobs(contract_job_id) ON DELETE CASCADE,
  applicant_account_id UUID NOT NULL REFERENCES accounts(account_id),
  status TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  CONSTRAINT contract_job_applications_status_valid CHECK (status IN ('Submitted', 'Withdrawn', 'Accepted', 'Rejected')),
  CONSTRAINT contract_job_applications_version_positive CHECK (version > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS contract_job_applications_submitted_unique
  ON contract_job_applications (contract_job_id, applicant_account_id)
  WHERE status = 'Submitted';

CREATE UNIQUE INDEX IF NOT EXISTS contract_job_applications_accepted_unique
  ON contract_job_applications (contract_job_id)
  WHERE status = 'Accepted';

CREATE TABLE IF NOT EXISTS contract_job_application_mutations (
  contract_job_application_mutation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  contract_job_application_id UUID NOT NULL REFERENCES contract_job_applications(contract_job_application_id) ON DELETE CASCADE,
  contract_job_id UUID NOT NULL REFERENCES contract_jobs(contract_job_id) ON DELETE CASCADE,
  mutation_kind TEXT NOT NULL,
  idempotency_key TEXT NOT NULL,
  resulting_version BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT contract_job_application_mutations_kind_valid CHECK (mutation_kind IN ('Submit', 'Withdraw', 'Accept')),
  CONSTRAINT contract_job_application_mutations_resulting_version_positive CHECK (resulting_version > 0),
  CONSTRAINT contract_job_application_mutations_idempotency_unique UNIQUE (contract_job_id, idempotency_key)
);
