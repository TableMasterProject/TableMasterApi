#!/usr/bin/env bash

set -Eeuo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-compose.sh"

backup_file="${1:-}"
if [[ -z "${backup_file}" || ! -s "${backup_file}" ]]; then
  echo "Usage : $0 /chemin/vers/tablemaster-AAAAmmjjTHHMMSSZ.dump" >&2
  exit 1
fi

restore_database="tablemaster_restore_test_$(date -u +%Y%m%d%H%M%S)"
started_at="$(date +%s)"

drop_restore_database() {
  tablemaster_compose exec -T -e RESTORE_DATABASE="${restore_database}" postgres sh -c \
    'dropdb --if-exists -U "$POSTGRES_USER" "$RESTORE_DATABASE"' >/dev/null 2>&1 || true
}
trap drop_restore_database EXIT

tablemaster_compose exec -T -e RESTORE_DATABASE="${restore_database}" postgres sh -c \
  'createdb -U "$POSTGRES_USER" "$RESTORE_DATABASE"'

tablemaster_compose exec -T -e RESTORE_DATABASE="${restore_database}" postgres sh -c \
  'pg_restore --exit-on-error --no-owner -U "$POSTGRES_USER" -d "$RESTORE_DATABASE"' \
  < "${backup_file}"

table_count="$(tablemaster_compose exec -T -e RESTORE_DATABASE="${restore_database}" postgres sh -c \
  'psql -At -U "$POSTGRES_USER" -d "$RESTORE_DATABASE" -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '\''public'\'';"' \
  | tr -d '[:space:]')"

if [[ ! "${table_count}" =~ ^[0-9]+$ || "${table_count}" -lt 1 ]]; then
  echo "La base restaurée ne contient aucune table publique." >&2
  exit 1
fi

duration_seconds="$(( $(date +%s) - started_at ))"
echo "Restauration validée dans ${restore_database} : ${table_count} tables, ${duration_seconds} seconde(s)."
