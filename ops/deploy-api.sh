#!/usr/bin/env bash

set -Eeuo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-compose.sh"

: "${API_IMAGE_TAG:?API_IMAGE_TAG est obligatoire}"
: "${APP_VERSION:?APP_VERSION est obligatoire}"
: "${SENTRY_RELEASE:?SENTRY_RELEASE est obligatoire}"

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
api_container_id="$(tablemaster_compose ps -q api 2>/dev/null || true)"
previous_image=""
previous_tag=""
previous_version="unknown"
previous_sentry_release="tablemaster-api@unknown"

if [[ -n "${api_container_id}" ]]; then
  previous_image="$(docker inspect --format '{{.Config.Image}}' "${api_container_id}")"
  previous_tag="${previous_image##*:}"
  previous_environment="$(docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "${api_container_id}")"
  previous_version="$(sed -n 's/^APP_VERSION=//p' <<< "${previous_environment}" | head -n 1)"
  previous_sentry_release="$(sed -n 's/^Sentry__Release=//p' <<< "${previous_environment}" | head -n 1)"
fi

wait_for_readiness() {
  local attempt
  for attempt in $(seq 1 30); do
    if curl -fsS --max-time 5 "${API_BASE_URL}/ready" | grep -q '"status":"Healthy"'; then
      return 0
    fi
    echo "API non prête, tentative ${attempt}/30."
    sleep 5
  done
  return 1
}

if [[ -n "${api_container_id}" ]]; then
  "${OPS_DIR}/backup-postgres.sh"
fi

write_deployment_metadata "${API_IMAGE_TAG}" "${APP_VERSION}" "${SENTRY_RELEASE}"
tablemaster_compose pull api
tablemaster_compose up -d postgres api

if wait_for_readiness && API_BASE_URL="${API_BASE_URL}" "${OPS_DIR}/smoke-test.sh"; then
  tablemaster_compose ps
  docker image prune -f
  echo "Déploiement validé : ${APP_VERSION} (${API_IMAGE_TAG})."
  exit 0
fi

echo "La nouvelle release n'est pas prête." >&2
tablemaster_compose logs api --tail=200 >&2 || true

if [[ -z "${previous_tag}" ]]; then
  echo "Aucune image précédente n'est disponible pour le rollback." >&2
  exit 1
fi

echo "Rollback vers ${previous_image}." >&2
write_deployment_metadata \
  "${previous_tag}" \
  "${previous_version:-unknown}" \
  "${previous_sentry_release:-tablemaster-api@unknown}"
tablemaster_compose up -d --no-deps api

if ! wait_for_readiness; then
  echo "Le rollback n'a pas restauré la readiness." >&2
  tablemaster_compose logs api --tail=200 >&2 || true
  exit 1
fi

echo "Rollback validé, le déploiement reste marqué en échec." >&2
exit 1
