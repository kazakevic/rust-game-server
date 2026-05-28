# uMod / Oxide & Plugin Installation

How the Oxide/uMod modding framework and its plugins get installed and updated. All
logic lives in `scripts/` and runs inside the `rust-server` container.

## Plugin sources

- **Local C# plugins** — any `*.cs` file in `./plugins/` (mounted read-only at
  `/plugins`). Currently the only custom plugin is `NpcAdmin.cs`, kept in
  `plugins/disabled/` (shelved); see [npc-admin.md](npc-admin.md).
- **External uMod plugins** — names listed one-per-line in `plugins/umod-plugins.txt`
  (lines starting with `#` are comments). These are downloaded from umod.org.

## Scripts

### `install-umod.sh`
Installs/updates the Oxide framework. Does a HEAD request to the uMod download URL,
compares the `ETag` against `oxide/.umod-version`, and only re-downloads + unzips into
`/rust` when it changed. Writes the new ETag as the version marker.

### `install-plugins.sh`
Two phases:
1. **Sync local plugins** — copies `/plugins/*.cs` into `oxide/plugins/`, tracking them
   in `oxide/.local-plugins`. Plugins previously synced but no longer present in
   `/plugins` are removed (clean uninstall).
2. **Download external plugins** — for each name in `umod-plugins.txt`, fetch
   `https://umod.org/plugins/<name>.json`, read `download_url` + `latest_release_version`,
   and download the `.cs` only when the version differs from the cached
   `oxide/.plugin-versions/<name>.version`. Missing plugins are warned and skipped.

### `update-umod.sh`
Force re-downloads the latest Oxide, re-runs `install-plugins.sh`, then triggers
`oxide.reload *` (via the `oxide.stdin` pipe if available, else instructs to reload via
RCON). Logs to `/rust/umod-update.log`.

### `setup-cron.sh`
Parses an hour interval from `UMOD_UPDATE_SCHEDULE` (cron-style, default `0 */6 * * *`)
and runs a background `sleep`/`update-umod.sh` loop — no root cron daemon needed.

## Triggers from the web dashboard

The uMod Plugins page (see [features.md](../web-admin/features.md)) edits
`umod-plugins.txt` and can `exec` `install-plugins.sh` in the container followed by
`oxide.reload *`. The dashboard also exposes "Reload All Plugins" and
"Re-download Plugins" controls.

## Idempotency

Both install scripts are version-aware (ETag for uMod, release version for plugins) so
re-running them — on every boot and on demand — only downloads what actually changed.
