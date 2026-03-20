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
