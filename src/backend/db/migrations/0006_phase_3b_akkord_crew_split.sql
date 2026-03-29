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
