#!/usr/bin/env bash
set -euo pipefail

export DATABASE_URL="postgresql://${POSTGRES_USER}:${POSTGRES_PASSWORD}@/${POSTGRES_DB}"

"/docker-entrypoint-initdb.d/db/scripts/apply-migrations.sh"
"/docker-entrypoint-initdb.d/db/scripts/apply-seed.sh"
