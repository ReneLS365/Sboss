CREATE TABLE IF NOT EXISTS loadout_sessions (
  loadout_session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
  level_seed_id UUID NOT NULL REFERENCES level_seeds(level_seed_id) ON DELETE CASCADE,
  max_capacity INTEGER NOT NULL,
  used_capacity INTEGER NOT NULL,
  is_complete BOOLEAN NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  version BIGINT NOT NULL,
  CONSTRAINT loadout_sessions_capacity_non_negative CHECK (max_capacity >= 0),
  CONSTRAINT loadout_sessions_used_capacity_non_negative CHECK (used_capacity >= 0),
  CONSTRAINT loadout_sessions_capacity_bounds CHECK (used_capacity <= max_capacity),
  CONSTRAINT loadout_sessions_version_positive CHECK (version > 0),
  CONSTRAINT loadout_sessions_account_seed_unique UNIQUE (account_id, level_seed_id)
);

CREATE TABLE IF NOT EXISTS loadout_session_items (
  loadout_session_item_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  loadout_session_id UUID NOT NULL REFERENCES loadout_sessions(loadout_session_id) ON DELETE CASCADE,
  item_code TEXT NOT NULL,
  quantity INTEGER NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  version BIGINT NOT NULL,
  CONSTRAINT loadout_session_items_quantity_non_negative CHECK (quantity >= 0),
  CONSTRAINT loadout_session_items_version_positive CHECK (version > 0),
  CONSTRAINT loadout_session_items_session_item_unique UNIQUE (loadout_session_id, item_code)
);

CREATE TABLE IF NOT EXISTS fog_of_war_states (
  fog_of_war_state_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
  level_seed_id UUID NOT NULL REFERENCES level_seeds(level_seed_id) ON DELETE CASCADE,
  reveal_key TEXT NOT NULL,
  revealed_at TIMESTAMPTZ NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  version BIGINT NOT NULL,
  CONSTRAINT fog_of_war_states_reveal_key_required CHECK (length(trim(reveal_key)) > 0),
  CONSTRAINT fog_of_war_states_version_positive CHECK (version > 0),
  CONSTRAINT fog_of_war_states_account_seed_key_unique UNIQUE (account_id, level_seed_id, reveal_key)
);
