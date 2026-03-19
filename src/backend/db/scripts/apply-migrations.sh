#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
db_dir="$(cd "${script_dir}/.." && pwd)"
migrations_dir="${db_dir}/migrations"

: "${DATABASE_URL:?DATABASE_URL must be set to a PostgreSQL connection string.}"

psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 <<'SQL'
CREATE TABLE IF NOT EXISTS schema_migrations (
  version TEXT PRIMARY KEY,
  checksum TEXT NOT NULL,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
SQL

shopt -s nullglob

for migration in "${migrations_dir}"/*.sql; do
  version="$(basename "${migration}" .sql)"
  checksum="$(sha256sum "${migration}" | awk '{print $1}')"
  applied_checksum="$(
    psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -tA \
      -c "SELECT checksum FROM schema_migrations WHERE version = '${version}';"
  )"

  if [[ -n "${applied_checksum}" ]]; then
    if [[ "${applied_checksum}" != "${checksum}" ]]; then
      echo "Checksum mismatch for migration ${version}." >&2
      exit 1
    fi

    echo "Skipping already applied migration ${version}."
    continue
  fi

  echo "Applying migration ${version}."
  psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -f "${migration}"
  psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 \
    -c "INSERT INTO schema_migrations (version, checksum) VALUES ('${version}', '${checksum}');"
done
