# Rust Game Server (entrypoint & boot)

The `rust-server` container's lifecycle is driven entirely by `entrypoint.sh`.

> For the upstream/manual Linux setup this automates (SteamCMD, AppID 258550, launch
> flags, ports, `server.cfg`), see
> [Reference: Rust Dedicated Server on Linux](rust-server-reference.md).

## Boot sequence (`entrypoint.sh`)

1. **Load settings from `/cfg/server-settings.json`** (written by the web dashboard).
   Each present key overrides the matching env var via `jq` — `serverName`, `mapSeed`,
   `worldSize`, `maxPlayers`, ports, `updateOnStart`, `umodEnabled`, `gslt`, etc.
   Env vars are the fallback when a key is absent.
2. **Intelligent update check** (modeled on Didstopia/rust-server) — decide whether to
   touch the Steam depot at all:
   - **No binary** → first install with `app_update 258550 validate`.
   - **`RUST_UPDATE_ON_START=0`** → skip the check, boot the installed build.
   - **`RUST_VALIDATE=1`** → force a full validate/repair (use to fix a corrupt/half-applied
     install, then set back to `0`).
   - **Otherwise** → compare the **installed build id** (from
     `steamapps/appmanifest_258550.acf`) with the **latest published build id** for
     `RUST_BRANCH` (default `public`, queried via `app_info_print`). Update only when they
     differ; an already-current server boots after a ~5s info check with no download/validate.

   All update paths share a retry loop with **self-heal**: on a persistent `state is 0x6`,
   it clears the stale manifest + staging and forces a clean validate. Failures fall back to
   booting an existing install; it aborts with a directory listing only if no binary exists.
   Knobs: `RUST_UPDATE_ON_START`, `RUST_BRANCH`, `RUST_VALIDATE`, `RUST_UPDATE_MAX_ATTEMPTS`,
   `RUST_STOP_TIMEOUT`. The same build-id comparison is exposed as `scripts/update-check.sh`
   (prints `{installed,latest,branch,updateAvailable}` JSON) so the web dashboard's
   **Check for updates** button can report status without restarting — keep the two in sync.
3. **uMod/Oxide install** — if `UMOD_ENABLED=1`, run `install-umod.sh` then
   `install-plugins.sh` (see [umod-oxide.md](../plugins/umod-oxide.md)).
4. **Copy `.cfg` files** — copy `/cfg/*.cfg` into
   `server/${RUST_SERVER_IDENTITY:-docker}/cfg/` (e.g. `users.cfg`, `server.cfg`).
5. **Steamworks symlink fix** — server code references
   `Facepunch.Steamworks.Win64.dll` but Linux ships only the `Posix` variant; symlink
   it so the companion app works.
6. **Launch RustDedicated** via `exec` with `-batchmode -nographics -load` and
   `+server.*` / `+rcon.*` / `+app.port` flags built from the resolved settings.

## Server identity & save data

`RUST_SERVER_IDENTITY` (default `docker`) names the save directory under
`/rust/server/<identity>/`. The dashboard's **Wipe** action deletes `*.map`, `*.sav`,
`*.sav.bak` there before restarting.

## Config files (`cfg/`)

Bind-mounted read-only into the game server at `/cfg` and read-write into the web app.

- `users.cfg` — owner/admin/moderator grants (e.g. `ownerid <steamid> "<name>" "owner"`).
- `server.cfg` — generated/edited gameplay config. The dashboard's Server Settings page
  rewrites a managed block here (pve, gamemode, tickrate, saveinterval, branding,
  idlekick, etc.) on save.
- `server-settings.json` — the dashboard's structured settings, consumed in step 1.

## RCON

The server runs web RCON (`rcon.web 1`) on `RUST_RCON_PORT` (default 28016) with
`RUST_RCON_PASSWORD`. This is the channel the web app's
[RconClient](../web-admin/backend.md) connects to.

## uMod auto-update

`setup-cron.sh` can run a background loop that periodically runs `update-umod.sh`
(interval parsed from `UMOD_UPDATE_SCHEDULE`). In the current entrypoint, uMod updates
are primarily triggered on demand from the web admin panel rather than on a cron.
