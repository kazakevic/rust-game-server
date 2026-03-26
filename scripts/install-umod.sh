#!/bin/bash
set -e

RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"
UMOD_DOWNLOAD_URL="https://umod.org/games/rust/download"
UMOD_VERSION_FILE="${RUST_SERVER_DIR}/oxide/.umod-version"

echo "==> Checking uMod (Oxide) framework..."

# Fetch the latest version info via HEAD request (ETag or Last-Modified)
LATEST_ETAG=$(curl -fsSI "${UMOD_DOWNLOAD_URL}" 2>/dev/null | grep -i '^etag:' | tr -d '\r' || true)

if [ -n "$LATEST_ETAG" ] && [ -f "$UMOD_VERSION_FILE" ] && [ -d "${RUST_SERVER_DIR}/oxide" ]; then
    INSTALLED_ETAG=$(cat "$UMOD_VERSION_FILE")
    if [ "$INSTALLED_ETAG" = "$LATEST_ETAG" ]; then
        echo "==> uMod is already up to date, skipping download."
        exit 0
    fi
fi

echo "==> Downloading uMod (Oxide) framework..."
TEMP_DIR=$(mktemp -d)
OXIDE_ZIP="${TEMP_DIR}/Oxide.Rust.zip"

if curl -fsSL -o "${OXIDE_ZIP}" "${UMOD_DOWNLOAD_URL}"; then
    echo "==> Extracting uMod into ${RUST_SERVER_DIR}..."
    unzip -o -q "${OXIDE_ZIP}" -d "${RUST_SERVER_DIR}"
    # Save version marker
    if [ -n "$LATEST_ETAG" ]; then
        mkdir -p "${RUST_SERVER_DIR}/oxide"
        echo "$LATEST_ETAG" > "$UMOD_VERSION_FILE"
    fi
    echo "==> uMod installed successfully."
else
    echo "WARNING: Failed to download uMod. Server will run without mod support."
fi

rm -rf "${TEMP_DIR}"
