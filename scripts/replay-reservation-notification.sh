#!/usr/bin/env bash
# Local/deployment operations only. libpq configuration: PGSERVICE + protected .pgpass.
# A concrete dead-letter task ID is required; no public HTTP endpoint.
set -euo pipefail
if [[ $# != 1 || ! "$1" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
  echo 'Usage: PGSERVICE=tablemaster-ops replay-reservation-notification.sh TASK_UUID' >&2
  exit 2
fi
: "${PGSERVICE:?Configure a local libpq service and protected credential file.}"
psql -X --no-password --set=ON_ERROR_STOP=1 --set=task_id="$1" <<'SQL'
BEGIN;
UPDATE "ReservationNotificationOutbox"
SET "State"='pending', "Attempts"=0, "DueAt"=now(), "LeaseId"=NULL, "LeaseUntil"=NULL, "LastError"=NULL
WHERE "Id"=:'task_id'::uuid AND "State"='dead'
RETURNING "Id", "State";
COMMIT;
SQL
