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
