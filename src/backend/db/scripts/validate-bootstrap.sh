#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

: "${POSTGRES_ADMIN_URL:?POSTGRES_ADMIN_URL must be set to an admin PostgreSQL connection string.}"

validation_db="sboss_phase1b_validation"
database_url="${POSTGRES_ADMIN_URL%/*}/${validation_db}"

psql "${POSTGRES_ADMIN_URL}" -v ON_ERROR_STOP=1 <<SQL
DROP DATABASE IF EXISTS ${validation_db};
CREATE DATABASE ${validation_db};
SQL

cleanup() {
  psql "${POSTGRES_ADMIN_URL}" -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS ${validation_db};" >/dev/null
}

trap cleanup EXIT

DATABASE_URL="${database_url}" "${script_dir}/apply-migrations.sh"
DATABASE_URL="${database_url}" "${script_dir}/apply-migrations.sh"
DATABASE_URL="${database_url}" "${script_dir}/apply-seed.sh"

psql "${database_url}" -v ON_ERROR_STOP=1 <<'SQL'
DO $$
DECLARE
  migration_count INTEGER;
  season_count INTEGER;
  level_seed_count INTEGER;
  match_result_count INTEGER;
BEGIN
  SELECT COUNT(*) INTO migration_count FROM schema_migrations;
  SELECT COUNT(*) INTO season_count FROM seasons;
  SELECT COUNT(*) INTO level_seed_count FROM level_seeds;
  SELECT COUNT(*) INTO match_result_count FROM match_results;

  IF migration_count <> 1 THEN
    RAISE EXCEPTION 'Expected 1 migration row, found %', migration_count;
  END IF;

  IF season_count <> 1 THEN
    RAISE EXCEPTION 'Expected 1 seeded season row, found %', season_count;
  END IF;

  IF level_seed_count <> 1 THEN
    RAISE EXCEPTION 'Expected 1 seeded level seed row, found %', level_seed_count;
  END IF;

  IF match_result_count <> 1 THEN
    RAISE EXCEPTION 'Expected 1 seeded match result row, found %', match_result_count;
  END IF;
END
$$;
SQL
