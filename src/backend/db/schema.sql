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
