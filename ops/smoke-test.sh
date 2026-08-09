#!/usr/bin/env bash

set -Eeuo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"

health_response="$(curl -fsS --max-time 10 "${API_BASE_URL}/health")"
ready_response="$(curl -fsS --max-time 10 "${API_BASE_URL}/ready")"

grep -q '"status":"Healthy"' <<< "${health_response}"
grep -q '"status":"Healthy"' <<< "${ready_response}"
grep -q '"version":' <<< "${health_response}"

echo "Smoke test réussi pour ${API_BASE_URL}."
