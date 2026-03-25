#!/bin/bash
set -e

RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"

echo "==> [$(date '+%Y-%m-%d %H:%M:%S')] Checking for uMod updates..."

# Download latest uMod
TEMP_DIR=$(mktemp -d)
OXIDE_ZIP="${TEMP_DIR}/Oxide.Rust.zip"

if curl -fsSL -o "${OXIDE_ZIP}" "https://umod.org/games/rust/download"; then
    echo "==> Extracting uMod into ${RUST_SERVER_DIR}..."
    unzip -o -q "${OXIDE_ZIP}" -d "${RUST_SERVER_DIR}"
    echo "==> uMod updated successfully."
else
    echo "WARNING: Failed to download uMod."
    rm -rf "${TEMP_DIR}"
    exit 1
fi

rm -rf "${TEMP_DIR}"

# Update plugins
/scripts/install-plugins.sh

# Reload plugins in Oxide
echo "==> Reloading Oxide plugins..."
if [ -p /rust/server/*/oxide/oxide.stdin 2>/dev/null ]; then
    echo "oxide.reload *" > /rust/server/*/oxide/oxide.stdin
else
    echo "==> Note: Could not auto-reload plugins. Run 'oxide.reload *' via RCON."
fi

echo "==> [$(date '+%Y-%m-%d %H:%M:%S')] uMod update complete."
