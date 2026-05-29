#!/bin/bash
set -e

RUST_SERVER_DIR="/rust"
STEAMCMD="/home/steam/steamcmd/steamcmd.sh"

cd "${RUST_SERVER_DIR}"

# Load settings from web admin JSON (env vars are fallbacks)
SETTINGS_FILE="/cfg/server-settings.json"
if [ -f "${SETTINGS_FILE}" ]; then
    echo "==> Loading settings from ${SETTINGS_FILE}..."
    _val=$(jq -r '.serverName // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_NAME="$_val"
    _val=$(jq -r '.serverIdentity // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_IDENTITY="$_val"
    _val=$(jq -r '.mapSeed // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_SEED="$_val"
    _val=$(jq -r '.worldSize // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_WORLDSIZE="$_val"
    _val=$(jq -r '.maxPlayers // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_MAXPLAYERS="$_val"
    _val=$(jq -r '.serverPort // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_PORT="$_val"
    _val=$(jq -r '.queryPort // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_QUERYPORT="$_val"
    _val=$(jq -r '.rconPort // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_RCON_PORT="$_val"
    _val=$(jq -r '.appPort // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_APP_PORT="$_val"
    _val=$(jq -r '.updateOnStart // empty' "${SETTINGS_FILE}")
    [ "$_val" = "true" ] && RUST_UPDATE_ON_START=1; [ "$_val" = "false" ] && RUST_UPDATE_ON_START=0
    _val=$(jq -r '.umodEnabled // empty' "${SETTINGS_FILE}")
    [ "$_val" = "true" ] && UMOD_ENABLED=1; [ "$_val" = "false" ] && UMOD_ENABLED=0
    _val=$(jq -r '.gslt // empty' "${SETTINGS_FILE}"); [ -n "$_val" ] && RUST_SERVER_GSLT="$_val"
fi

# Update server if enabled or if server binary is missing (first run).
# Rust's depot frequently fails the first SteamCMD pass with "Error! App '258550' state
# is 0x6" — a transient depot error (or low disk). Retry a few times before giving up.
# Running steamcmd as the `until` condition keeps `set -e` from killing the script on a
# failed attempt, so a persistent failure can still fall back to an existing install
# instead of crash-looping the container.
if [ "${RUST_UPDATE_ON_START:-1}" = "1" ] || [ ! -f "./RustDedicated" ]; then
    echo "==> Updating Rust Dedicated Server (AppID 258550)..."
    _attempt=1
    _max_attempts="${RUST_UPDATE_MAX_ATTEMPTS:-5}"
    _cleaned=0
    until ${STEAMCMD} \
        +@sSteamCmdForcePlatformType linux \
        +force_install_dir "${RUST_SERVER_DIR}" \
        +login anonymous \
        +app_update 258550 validate \
        +quit; do
        if [ "${_attempt}" -ge "${_max_attempts}" ]; then
            echo "==> SteamCMD update failed after ${_max_attempts} attempts."
            echo "==> Likely causes: low disk space (check 'df -h') or a Steam depot outage."
            break
        fi
        # A persistent "state is 0x6" (reconfiguring -> unknown, no download) means a stale
        # or half-applied install manifest SteamCMD can't reconcile. After the first retry,
        # clear the manifest + staging so the next pass does a clean validate/repair. Game
        # saves live in server/<identity> and are NOT touched.
        if [ "${_attempt}" -ge 2 ] && [ "${_cleaned}" -eq 0 ]; then
            echo "==> Clearing stale Steam app manifest + staging for a clean re-validate..."
            rm -f "${RUST_SERVER_DIR}/steamapps/appmanifest_258550.acf"
            rm -rf "${RUST_SERVER_DIR}/steamapps/downloading" "${RUST_SERVER_DIR}/steamapps/temp"
            _cleaned=1
        fi
        echo "==> SteamCMD update failed (attempt ${_attempt}/${_max_attempts}); retrying in 15s..."
        df -h "${RUST_SERVER_DIR}" 2>/dev/null || true
        _attempt=$((_attempt + 1))
        sleep 15
    done
else
    echo "==> Skipping server update (RUST_UPDATE_ON_START=0)."
fi

if [ ! -f "./RustDedicated" ]; then
    echo "ERROR: RustDedicated binary not found in ${RUST_SERVER_DIR}"
    echo "SteamCMD could not download the server files. Check disk space (df -h) and retry,"
    echo "or set RUST_UPDATE_ON_START=0 once a good install exists to skip the update."
    ls -la "${RUST_SERVER_DIR}"
    exit 1
fi

echo "==> RustDedicated binary present — continuing boot."

# Install uMod (Oxide) if enabled
if [ "${UMOD_ENABLED:-1}" = "1" ]; then
    /scripts/install-umod.sh
    /scripts/install-plugins.sh
else
    echo "==> uMod disabled, skipping."
fi

# uMod auto-update is handled via web admin panel

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
    +server.gameservertoken "${RUST_SERVER_GSLT:-}" \
    +app.port "${RUST_APP_PORT:-28082}" \
    "$@"
