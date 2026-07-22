#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
assets_dir="$repo_root/apps/mobile/assets"

if [[ ! -f "$assets_dir/icon.png" ]]; then
  echo "Missing $assets_dir/icon.png"
  echo "Add a 1024x1024 source icon before refreshing Capacitor assets."
  exit 0
fi

cd "$repo_root/apps/mobile"
pnpm dlx @capacitor/assets generate --assetPath "$assets_dir"
