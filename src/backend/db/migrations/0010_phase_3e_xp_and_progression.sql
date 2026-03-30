CREATE TABLE IF NOT EXISTS account_progression (
  account_id UUID PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
  total_xp BIGINT NOT NULL DEFAULT 0,
  level INTEGER NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  version BIGINT NOT NULL DEFAULT 1,
  CONSTRAINT account_progression_total_xp_non_negative CHECK (total_xp >= 0),
  CONSTRAINT account_progression_level_positive CHECK (level > 0),
  CONSTRAINT account_progression_version_positive CHECK (version > 0)
);

CREATE TABLE IF NOT EXISTS progression_xp_awards (
  progression_xp_award_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
  match_result_id UUID NOT NULL REFERENCES match_results(match_result_id) ON DELETE CASCADE,
  level_seed_id UUID NOT NULL REFERENCES level_seeds(level_seed_id) ON DELETE CASCADE,
  base_xp INTEGER NOT NULL,
  difficulty_bps INTEGER NOT NULL,
  performance_bonus_xp INTEGER NOT NULL,
  xp_awarded INTEGER NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT progression_xp_awards_unique_match_per_account UNIQUE (account_id, match_result_id),
  CONSTRAINT progression_xp_awards_base_xp_positive CHECK (base_xp > 0),
  CONSTRAINT progression_xp_awards_difficulty_bps_positive CHECK (difficulty_bps > 0),
  CONSTRAINT progression_xp_awards_performance_bonus_non_negative CHECK (performance_bonus_xp >= 0),
  CONSTRAINT progression_xp_awards_xp_awarded_positive CHECK (xp_awarded > 0)
);
