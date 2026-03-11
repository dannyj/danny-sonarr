#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

if [[ -z "${SONARR_TARGET_RUNTIME:-}" || -z "${SONARR_PLATFORM:-}" ]]; then
  case "$(uname -m)" in
    arm64|aarch64)
      export SONARR_TARGET_RUNTIME="${SONARR_TARGET_RUNTIME:-linux-arm64}"
      export SONARR_PLATFORM="${SONARR_PLATFORM:-linux/arm64}"
      ;;
    x86_64|amd64)
      export SONARR_TARGET_RUNTIME="${SONARR_TARGET_RUNTIME:-linux-x64}"
      export SONARR_PLATFORM="${SONARR_PLATFORM:-linux/amd64}"
      ;;
  esac
fi

mkdir -p \
  "${repo_root}/data/config" \
  "${repo_root}/data/tv" \
  "${repo_root}/data/downloads"

cd "${repo_root}"

exec docker compose up --build "$@"
