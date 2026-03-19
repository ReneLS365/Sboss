#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
db_dir="$(cd "${script_dir}/.." && pwd)"

: "${DATABASE_URL:?DATABASE_URL must be set to a PostgreSQL connection string.}"

psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -f "${db_dir}/seed.sql"
