#!/bin/bash
set -e

RUST_SERVER_DIR="/rust"
STEAMCMD="/home/steam/steamcmd/steamcmd.sh"

cd "${RUST_SERVER_DIR}"

# Update server if enabled or if server binary is missing (first run)
if [ "${RUST_UPDATE_ON_START:-1}" = "1" ] || [ ! -f "./RustDedicated" ]; then
    echo "==> Updating Rust Dedicated Server (AppID 258550)..."
    ${STEAMCMD} \
        +@sSteamCmdForcePlatformType linux \
        +force_install_dir "${RUST_SERVER_DIR}" \
        +login anonymous \
        +app_update 258550 validate \
        +quit
else
    echo "==> Skipping server update (RUST_UPDATE_ON_START=0)."
fi

if [ ! -f "./RustDedicated" ]; then
    echo "ERROR: RustDedicated binary not found in ${RUST_SERVER_DIR}"
    echo "SteamCMD may have failed to download the server files."
    ls -la "${RUST_SERVER_DIR}"
    exit 1
fi

# Install uMod (Oxide) if enabled
if [ "${UMOD_ENABLED:-1}" = "1" ]; then
    /scripts/install-umod.sh
    /scripts/install-plugins.sh
else
    echo "==> uMod disabled, skipping."
fi

# Setup uMod auto-update cron if enabled
if [ "${UMOD_ENABLED:-1}" = "1" ] && [ "${UMOD_AUTO_UPDATE:-1}" = "1" ]; then
    /scripts/setup-cron.sh
    cron
fi

# Copy server cfg files (users.cfg, etc.)
SERVER_CFG_DIR="${RUST_SERVER_DIR}/server/${RUST_SERVER_IDENTITY:-docker}/cfg"
mkdir -p "${SERVER_CFG_DIR}"
if [ -d "/cfg" ] && [ "$(ls -A /cfg/*.cfg 2>/dev/null)" ]; then
    echo "==> Copying server cfg files..."
    cp /cfg/*.cfg "${SERVER_CFG_DIR}/"
fi

# Fix Steamworks assembly reference — server code references Win64 but Linux only has Posix
if [ -f "Facepunch.Steamworks.Posix.dll" ] && [ ! -f "Facepunch.Steamworks.Win64.dll" ]; then
    echo "==> Symlinking Facepunch.Steamworks.Posix.dll -> Win64 for companion app support..."
    ln -sf Facepunch.Steamworks.Posix.dll Facepunch.Steamworks.Win64.dll
fi

echo "==> Starting Rust Dedicated Server..."
exec ./RustDedicated \
    -batchmode \
    -nographics \
    -load \
    +server.port "${RUST_SERVER_PORT:-28015}" \
    +server.queryport "${RUST_SERVER_QUERYPORT:-28017}" \
    +rcon.port "${RUST_RCON_PORT:-28016}" \
    +rcon.web "${RUST_RCON_WEB:-1}" \
    +rcon.password "${RUST_RCON_PASSWORD:-changeme}" \
    +server.hostname "${RUST_SERVER_NAME:-Rust Server}" \
    +server.identity "${RUST_SERVER_IDENTITY:-docker}" \
    +server.seed "${RUST_SERVER_SEED:-12345}" \
    +server.worldsize "${RUST_SERVER_WORLDSIZE:-3500}" \
    +server.maxplayers "${RUST_SERVER_MAXPLAYERS:-100}" \
    +server.secure 1 \
    +app.port "${RUST_APP_PORT:-28082}" \
    "$@"
