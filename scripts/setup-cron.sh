#!/bin/bash
set -e

# Parse the update interval from the cron schedule (hours between runs)
# Default schedule "0 */6 * * *" = every 6 hours
UMOD_UPDATE_SCHEDULE="${UMOD_UPDATE_SCHEDULE:-0 */6 * * *}"

# Extract hour interval from cron expression (e.g. "*/6" -> 6, "*" -> 1)
HOUR_FIELD=$(echo "${UMOD_UPDATE_SCHEDULE}" | awk '{print $2}')
if [[ "${HOUR_FIELD}" =~ ^\*/([0-9]+)$ ]]; then
    INTERVAL_HOURS="${BASH_REMATCH[1]}"
else
    INTERVAL_HOURS=6
fi
INTERVAL_SECONDS=$((INTERVAL_HOURS * 3600))

echo "==> Setting up uMod auto-update loop (every ${INTERVAL_HOURS}h)..."

# Run update loop in background (no root-requiring cron daemon needed)
(
    while true; do
        sleep "${INTERVAL_SECONDS}"
        /scripts/update-umod.sh >> /rust/umod-update.log 2>&1 || true
    done
) &

echo "==> uMod auto-update scheduled (PID: $!)."
