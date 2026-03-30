-- Phase 1B compatibility snapshot.
-- Canonical schema ownership now lives in db/migrations and is applied via db/scripts/apply-migrations.sh.

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE IF NOT EXISTS schema_migrations (
  version TEXT PRIMARY KEY,
  checksum TEXT NOT NULL,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS accounts (
  account_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  external_ref TEXT NOT NULL UNIQUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS player_profiles (
  player_profile_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  display_name TEXT NOT NULL,
  experience INTEGER NOT NULL DEFAULT 0,
  level INTEGER NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS inventory_items (
  inventory_item_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  item_code TEXT NOT NULL,
  quantity INTEGER NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS seasons (
  season_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  starts_at TIMESTAMPTZ NOT NULL,
  ends_at TIMESTAMPTZ NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS level_seeds (
  level_seed_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  seed_value TEXT NOT NULL,
  biome TEXT NOT NULL,
  template TEXT NOT NULL,
  objective TEXT NOT NULL,
  modifiers_json JSONB NOT NULL DEFAULT '{}'::jsonb,
  par_time_ms INTEGER NOT NULL,
  gold_time_ms INTEGER NOT NULL,
  version INTEGER NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS cosmetic_unlocks (
  cosmetic_unlock_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  cosmetic_code TEXT NOT NULL,
  unlocked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS account_balances (
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  currency_code TEXT NOT NULL,
  balance BIGINT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (account_id, currency_code),
  CONSTRAINT account_balances_balance_non_negative CHECK (balance >= 0)
);

CREATE TABLE IF NOT EXISTS economy_transactions (
  economy_transaction_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  currency_code TEXT NOT NULL,
  idempotency_key TEXT NOT NULL,
  amount_delta BIGINT NOT NULL,
  resulting_balance BIGINT NOT NULL,
  resulting_balance_version BIGINT NOT NULL,
  reason TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  CONSTRAINT economy_transactions_amount_non_zero CHECK (amount_delta <> 0),
  CONSTRAINT economy_transactions_resulting_balance_non_negative CHECK (resulting_balance >= 0),
  CONSTRAINT economy_transactions_resulting_balance_version_positive CHECK (resulting_balance_version > 0),
  CONSTRAINT economy_transactions_idempotency_unique UNIQUE (account_id, idempotency_key),
  CONSTRAINT economy_transactions_balance_fk
    FOREIGN KEY (account_id, currency_code)
    REFERENCES account_balances(account_id, currency_code)
);

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
  CONSTRAINT contract_job_application_mutations_idempotency_unique UNIQUE (contract_job_id, mutation_kind, idempotency_key)
);

CREATE TABLE IF NOT EXISTS match_results (
  match_result_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  season_id UUID NOT NULL REFERENCES seasons(season_id),
  level_seed_id UUID NOT NULL REFERENCES level_seeds(level_seed_id),
  score INTEGER NOT NULL,
  clear_time_ms INTEGER NOT NULL,
  combo_max INTEGER NOT NULL,
  penalties INTEGER NOT NULL,
  validation_status TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS crews (
  crew_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  owner_account_id UUID NOT NULL REFERENCES accounts(account_id),
  name TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  CONSTRAINT crews_name_length CHECK (char_length(trim(name)) BETWEEN 1 AND 64),
  CONSTRAINT crews_version_positive CHECK (version > 0)
);

CREATE TABLE IF NOT EXISTS crew_members (
  crew_id UUID NOT NULL REFERENCES crews(crew_id) ON DELETE CASCADE,
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  role TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (crew_id, account_id),
  CONSTRAINT crew_members_role_valid CHECK (role IN ('Svend', 'Laerling')),
  CONSTRAINT crew_members_version_positive CHECK (version > 0)
);

CREATE TABLE IF NOT EXISTS crew_payout_settlements (
  crew_id UUID NOT NULL REFERENCES crews(crew_id) ON DELETE CASCADE,
  owner_account_id UUID NOT NULL REFERENCES accounts(account_id),
  idempotency_key TEXT NOT NULL,
  currency_code TEXT NOT NULL,
  reason TEXT NOT NULL,
  gross_amount BIGINT NOT NULL,
  crew_share_ratio_bps INTEGER NOT NULL,
  crew_share_amount BIGINT NOT NULL,
  company_share_amount BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (crew_id, idempotency_key),
  CONSTRAINT crew_payout_settlements_currency_not_blank CHECK (char_length(trim(currency_code)) BETWEEN 1 AND 32),
  CONSTRAINT crew_payout_settlements_reason_not_blank CHECK (char_length(trim(reason)) BETWEEN 1 AND 128),
  CONSTRAINT crew_payout_settlements_gross_positive CHECK (gross_amount > 0),
  CONSTRAINT crew_payout_settlements_ratio_valid CHECK (crew_share_ratio_bps BETWEEN 0 AND 10000),
  CONSTRAINT crew_payout_settlements_crew_share_non_negative CHECK (crew_share_amount >= 0),
  CONSTRAINT crew_payout_settlements_company_share_non_negative CHECK (company_share_amount >= 0),
  CONSTRAINT crew_payout_settlements_share_sum_matches_gross CHECK (crew_share_amount + company_share_amount = gross_amount)
);

CREATE TABLE IF NOT EXISTS crew_payout_settlement_members (
  crew_id UUID NOT NULL,
  idempotency_key TEXT NOT NULL,
  account_id UUID NOT NULL REFERENCES accounts(account_id),
  role TEXT NOT NULL,
  role_weight INTEGER NOT NULL,
  amount BIGINT NOT NULL,
  PRIMARY KEY (crew_id, idempotency_key, account_id),
  CONSTRAINT crew_payout_settlement_members_settlement_fk
    FOREIGN KEY (crew_id, idempotency_key)
    REFERENCES crew_payout_settlements(crew_id, idempotency_key)
    ON DELETE CASCADE,
  CONSTRAINT crew_payout_settlement_members_role_valid CHECK (role IN ('Svend', 'Laerling')),
  CONSTRAINT crew_payout_settlement_members_role_weight_positive CHECK (role_weight > 0),
  CONSTRAINT crew_payout_settlement_members_amount_non_negative CHECK (amount >= 0)
);
