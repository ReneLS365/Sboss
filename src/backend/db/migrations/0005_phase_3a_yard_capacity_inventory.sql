CREATE TABLE IF NOT EXISTS yard_states (
  account_id UUID PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
  max_capacity INTEGER NOT NULL CHECK (max_capacity >= 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS inventory_items_account_item_code_unique
  ON inventory_items (account_id, item_code);
