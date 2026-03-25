#!/bin/bash
set -e

RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"
UMOD_DOWNLOAD_URL="https://umod.org/games/rust/download"

echo "==> Installing/Updating uMod (Oxide) framework..."

TEMP_DIR=$(mktemp -d)
OXIDE_ZIP="${TEMP_DIR}/Oxide.Rust.zip"

if curl -fsSL -o "${OXIDE_ZIP}" "${UMOD_DOWNLOAD_URL}"; then
    echo "==> Extracting uMod into ${RUST_SERVER_DIR}..."
    unzip -o -q "${OXIDE_ZIP}" -d "${RUST_SERVER_DIR}"
    echo "==> uMod installed successfully."
else
    echo "WARNING: Failed to download uMod. Server will run without mod support."
fi

rm -rf "${TEMP_DIR}"
