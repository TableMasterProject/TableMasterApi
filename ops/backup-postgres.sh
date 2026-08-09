#!/usr/bin/env bash

set -Eeuo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-compose.sh"

BACKUP_DIRECTORY="${BACKUP_DIRECTORY:-/opt/tablemaster/backups/postgres}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${BACKUP_DIRECTORY}/tablemaster-${timestamp}.dump"
temporary_file="${backup_file}.partial"

mkdir -p "${BACKUP_DIRECTORY}"
chmod 700 "${BACKUP_DIRECTORY}"

cleanup() {
  rm -f "${temporary_file}"
}
trap cleanup EXIT

tablemaster_compose exec -T postgres sh -c \
  'exec pg_dump -Fc -U "$POSTGRES_USER" "$POSTGRES_DB"' > "${temporary_file}"

if [[ ! -s "${temporary_file}" ]]; then
  echo "La sauvegarde PostgreSQL générée est vide." >&2
  exit 1
fi

chmod 600 "${temporary_file}"
mv "${temporary_file}" "${backup_file}"
find "${BACKUP_DIRECTORY}" -maxdepth 1 -type f -name 'tablemaster-*.dump' \
  -mtime "+${BACKUP_RETENTION_DAYS}" -delete
find "${BACKUP_DIRECTORY}" -maxdepth 1 -type f -name 'tablemaster-*.dump.sha256' \
  -mtime "+${BACKUP_RETENTION_DAYS}" -delete

sha256sum "${backup_file}" > "${backup_file}.sha256"
echo "Sauvegarde créée : ${backup_file}"
