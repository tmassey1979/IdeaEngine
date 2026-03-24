#!/usr/bin/env bash
set -euo pipefail

docker compose up -d
trap 'docker compose down' EXIT

curl --fail --silent http://localhost:5080/health >/dev/null
curl --fail --silent http://localhost:5080/api/identity >/dev/null
docker compose ps