#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

echo "Building responsive web shell..."
pnpm --filter @bankers-seat/web build

if [[ ! -d "apps/mobile/android/app" && ! -d "apps/mobile/ios/App" ]]; then
  echo "Capacitor platform projects have not been added yet."
  echo "Run one of the following commands first:"
  echo "  pnpm --filter @bankers-seat/mobile exec cap add android"
  echo "  pnpm --filter @bankers-seat/mobile exec cap add ios"
  exit 0
fi

echo "Syncing Capacitor shell..."
pnpm --dir apps/mobile exec cap sync
