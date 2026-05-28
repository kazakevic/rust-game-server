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
2. **SteamCMD update** — if `RUST_UPDATE_ON_START=1` (default) *or* the `RustDedicated`
   binary is missing, run `app_update 258550 validate`. Otherwise skip. Aborts with a
   directory listing if the binary still isn't present afterward.
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
