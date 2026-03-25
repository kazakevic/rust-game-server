#!/bin/bash
set -e

UMOD_UPDATE_SCHEDULE="${UMOD_UPDATE_SCHEDULE:-0 */6 * * *}"

echo "==> Setting up uMod auto-update cron (${UMOD_UPDATE_SCHEDULE})..."

CRON_LINE="${UMOD_UPDATE_SCHEDULE} /scripts/update-umod.sh >> /rust/umod-update.log 2>&1"

(crontab -l 2>/dev/null || true; echo "${CRON_LINE}") | crontab -

echo "==> Cron job installed."
