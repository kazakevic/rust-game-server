#!/bin/bash
set -e

RUST_SERVER_DIR="/rust"
STEAMCMD="/home/steam/steamcmd/steamcmd.sh"

cd "${RUST_SERVER_DIR}"

# Seed /cfg from image defaults on first boot. /cfg is a named volume (rust-cfg) so it
# persists across Dokploy redeploys; a fresh volume starts empty, so copy any baked-in
# defaults (e.g. users.cfg) that are missing. copy-if-not-exists — never clobber
# web-admin-written settings (server-settings.json/server.cfg) or live admin edits.
if [ -d /seed-cfg ]; then
    mkdir -p /cfg
    for _seed in /seed-cfg/*; do
        [ -e "$_seed" ] || continue
        _dest="/cfg/$(basename "$_seed")"
        if [ ! -e "$_dest" ]; then
            echo "==> Seeding default $(basename "$_seed") into /cfg..."
            cp "$_seed" "$_dest"
        fi
    done
fi

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

# ─── Intelligent update check (modeled on Didstopia/rust-server) ──────────────
# Only touch the Steam depot when the build actually changed: compare the installed build
# id (from the local app manifest) against the latest published build id (queried cheaply
# via `app_info_print`). A normal restart with no new build does a ~5s info check and boots
# straight away — no re-download, no full re-validate. `validate` is reserved for first
# install, an explicit RUST_VALIDATE=1, and the self-heal path.
APP_ID=258550
RUST_BRANCH="${RUST_BRANCH:-public}"
MANIFEST="${RUST_SERVER_DIR}/steamapps/appmanifest_${APP_ID}.acf"

# Build id currently installed, read from the Steam app manifest (empty if none).
installed_buildid() {
    [ -f "${MANIFEST}" ] || return 0
    grep '"buildid"' "${MANIFEST}" 2>/dev/null | head -1 | grep -oE '[0-9]+' | head -1
}

# Latest published build id for the configured branch (empty if Steam is unreachable).
latest_buildid() {
    ${STEAMCMD} +login anonymous +app_info_update 1 +app_info_print "${APP_ID}" +quit 2>/dev/null \
        | awk -v branch="\"${RUST_BRANCH}\"" '
            /"branches"/ { inbr = 1 }
            inbr && $0 ~ branch { inb = 1 }
            inb && /"buildid"/ { gsub(/[^0-9]/, ""); print; exit }'
}

# Run app_update with retry + self-heal. $1 = "validate" (or empty for a plain delta update).
# Returns non-zero on persistent failure; callers neutralize it so set -e can't crash-loop.
run_steamcmd_update() {
    local validate="$1" attempt=1 cleaned=0
    local max_attempts="${RUST_UPDATE_MAX_ATTEMPTS:-5}"
    until ${STEAMCMD} \
        +@sSteamCmdForcePlatformType linux \
        +force_install_dir "${RUST_SERVER_DIR}" \
        +login anonymous \
        +app_update "${APP_ID}" ${validate} \
        +quit; do
        if [ "${attempt}" -ge "${max_attempts}" ]; then
            echo "==> SteamCMD update failed after ${max_attempts} attempts."
            echo "==> Likely causes: low disk space (check 'df -h') or a Steam depot outage."
            return 1
        fi
        # A persistent "state is 0x6" means a stale / half-applied manifest SteamCMD can't
        # reconcile. Clear it + staging and force a full validate to repair. Game saves in
        # server/<identity> are NOT touched.
        if [ "${attempt}" -ge 2 ] && [ "${cleaned}" -eq 0 ]; then
            echo "==> Clearing stale Steam app manifest + staging and forcing a validate repair..."
            rm -f "${MANIFEST}"
            rm -rf "${RUST_SERVER_DIR}/steamapps/downloading" "${RUST_SERVER_DIR}/steamapps/temp"
            validate="validate"
            cleaned=1
        fi
        echo "==> SteamCMD update failed (attempt ${attempt}/${max_attempts}); retrying in 15s..."
        df -h "${RUST_SERVER_DIR}" 2>/dev/null || true
        attempt=$((attempt + 1))
        sleep 15
    done
    return 0
}

if [ ! -f "./RustDedicated" ]; then
    echo "==> No server binary found — performing first install (with validate)..."
    run_steamcmd_update "validate" || echo "==> First install did not complete cleanly."
elif [ "${RUST_UPDATE_ON_START:-1}" = "0" ]; then
    echo "==> Update check disabled (RUST_UPDATE_ON_START=0); booting installed build."
elif [ "${RUST_VALIDATE:-0}" = "1" ]; then
    echo "==> RUST_VALIDATE=1 — forcing a full validate/repair..."
    run_steamcmd_update "validate" || echo "==> Validate did not complete; booting existing install."
else
    _installed="$(installed_buildid || true)"
    echo "==> Checking for updates (branch: ${RUST_BRANCH}, installed build: ${_installed:-unknown})..."
    _latest="$(latest_buildid || true)"
    if [ -z "${_latest}" ]; then
        echo "==> Could not reach Steam for the latest build id; keeping current install."
    elif [ -n "${_installed}" ] && [ "${_installed}" = "${_latest}" ]; then
        echo "==> Already on the latest build (${_installed}); skipping update."
    else
        echo "==> Update available (${_installed:-none} -> ${_latest}); updating with validate..."
        run_steamcmd_update "validate" || echo "==> Update did not complete; booting existing install."
    fi
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
