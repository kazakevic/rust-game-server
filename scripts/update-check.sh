#!/bin/bash
# Reports Rust Dedicated Server build ids for the dashboard's "Check for updates" button.
# Prints a single JSON line: {"installed","latest","branch","updateAvailable"}.
#
# Run inside the rust-server container (the web admin execs it over the Docker socket).
# The build-id logic mirrors entrypoint.sh's intelligent update check — keep them in sync.
set -e

APP_ID="${RUST_APP_ID:-258550}"
RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"
RUST_BRANCH="${RUST_BRANCH:-public}"
STEAMCMD="${STEAMCMD:-/home/steam/steamcmd/steamcmd.sh}"
MANIFEST="${RUST_SERVER_DIR}/steamapps/appmanifest_${APP_ID}.acf"

# Installed build id from the local app manifest (empty if not installed).
installed=""
if [ -f "${MANIFEST}" ]; then
    installed="$(grep '"buildid"' "${MANIFEST}" 2>/dev/null | head -1 | grep -oE '[0-9]+' | head -1)"
fi

# Latest published build id for the branch (empty if Steam is unreachable). steamcmd's
# noisy output is confined to the command substitution + filtered by awk, so this script's
# stdout is only the final JSON line.
latest="$(${STEAMCMD} +login anonymous +app_info_update 1 +app_info_print "${APP_ID}" +quit 2>/dev/null \
    | awk -v branch="\"${RUST_BRANCH}\"" '
        /"branches"/ { inbr = 1 }
        inbr && $0 ~ branch { inb = 1 }
        inb && /"buildid"/ { gsub(/[^0-9]/, ""); print; exit }')"

update_available=false
if [ -n "${latest}" ] && [ "${installed}" != "${latest}" ]; then
    update_available=true
fi

printf '{"installed":"%s","latest":"%s","branch":"%s","updateAvailable":%s}\n' \
    "${installed}" "${latest}" "${RUST_BRANCH}" "${update_available}"
