#!/usr/bin/env bash
set -euo pipefail

docker compose config >/dev/null
docker compose ps >/dev/null