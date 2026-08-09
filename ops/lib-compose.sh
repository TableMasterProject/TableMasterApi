#!/usr/bin/env bash

set -Eeuo pipefail

OPS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_DIR="$(cd "${OPS_DIR}/.." && pwd)"

if [[ -f "${REPOSITORY_DIR}/docker-compose.yml" ]]; then
  COMPOSE_FILE="${REPOSITORY_DIR}/docker-compose.yml"
elif [[ -f "${REPOSITORY_DIR}/TableMasterApi/docker-compose.yml" ]]; then
  COMPOSE_FILE="${REPOSITORY_DIR}/TableMasterApi/docker-compose.yml"
else
  echo "Aucun docker-compose.yml TableMaster n'a été trouvé." >&2
  exit 1
fi

COMPOSE_DIRECTORY="$(dirname "${COMPOSE_FILE}")"

if [[ -f "${REPOSITORY_DIR}/.env" ]]; then
  SECRETS_ENV_FILE="${REPOSITORY_DIR}/.env"
elif [[ -f "${COMPOSE_DIRECTORY}/.env" ]]; then
  SECRETS_ENV_FILE="${COMPOSE_DIRECTORY}/.env"
else
  echo "Le fichier .env de production est introuvable." >&2
  exit 1
fi

DEPLOYMENT_ENV_FILE="${DEPLOYMENT_ENV_FILE:-${REPOSITORY_DIR}/.deployment.env}"

tablemaster_compose() {
  local arguments=(--env-file "${SECRETS_ENV_FILE}")

  if [[ -f "${DEPLOYMENT_ENV_FILE}" ]]; then
    arguments+=(--env-file "${DEPLOYMENT_ENV_FILE}")
  fi

  docker compose "${arguments[@]}" -f "${COMPOSE_FILE}" "$@"
}

write_deployment_metadata() {
  local image_tag="$1"
  local app_version="$2"
  local sentry_release="$3"
  local temporary_file

  temporary_file="$(mktemp "${DEPLOYMENT_ENV_FILE}.XXXXXX")"
  chmod 600 "${temporary_file}"
  {
    printf 'API_IMAGE_TAG=%s\n' "${image_tag}"
    printf 'APP_VERSION=%s\n' "${app_version}"
    printf 'SENTRY_RELEASE=%s\n' "${sentry_release}"
  } > "${temporary_file}"
  mv "${temporary_file}" "${DEPLOYMENT_ENV_FILE}"
}
