# Web Admin — Features & Routes

What each page does and the API endpoints behind it. All routes are gated by
`authGuard` (see [backend.md](backend.md)). Defined in `web/src/index.ts`.

## Auth & navigation
- `GET /login`, `POST /login`, `POST /logout` — credential login, 24h cookie session.
- `GET /` — redirect to `/dashboard` (or `/login` if unauthenticated).

## Dashboard — `GET /dashboard`
Status/stats tiles (running state, uptime, CPU, memory) plus live `serverinfo` via RCON
(hostname, players, map, FPS). Server controls and (when running) plugin/world/weather
controls. **Live, no full-page reload:** the page polls `GET /api/server/status` every 5s
and updates tiles + swaps the Start↔Stop controls in place (uptime ticks every 1s).
- `GET /api/server/status` — JSON `{ status, stats, serverInfo }` for the live poll.
- `GET /api/server/update/check` — **Check for updates** button: execs
  `scripts/update-check.sh` in the game-server container to compare the installed Rust
  build id against the latest published one, returning `{ installed, latest, branch,
  updateAvailable }` (or `{ error }` if the server isn't running). When an update exists the
  dashboard shows an **Update & restart** button, which just triggers `restart` — the
  intelligent entrypoint downloads the new build on boot.
- `POST /api/server/restart | stop | start` — container lifecycle. **Non-blocking:** each
  returns `{ ok: true }` immediately and runs the Docker op in the background. The button
  enters a "Stopping…/Restarting…" pending state with a progress banner; the poll detects
  completion (stop → not running; restart/wipe → `startedAt` changed) and restores controls
  (5-min safety timeout). Failures are written to the web log.
- `POST /api/server/wipe` — delete map/save files for the active identity, then restart
  (also non-blocking).
- `POST /api/plugins/reload-all` — `oxide.reload *`.
- `POST /api/plugins/redownload` — run `install-plugins.sh` then reload.
- `POST /api/world/set-day | set-night` — `env.time`.
- `POST /api/weather/clear | rain | fog | storm` — `weather.*` commands.

## RCON Console — `GET /rcon`
Live command terminal with history plus a grouped, searchable command library
(server info, control, config, world/time, weather, …).
- `POST /api/rcon` — run an arbitrary RCON command, return its output.

## Players — `GET /players`
Online player list with a teleport-to-player action.
- `GET /api/players` — `playerlist` (parsed JSON) via RCON.
- `POST /api/players/teleport` — `teleport <steamId>`.

## NPC Manager — `GET /npcs`
Spawn/configure/remove AI NPCs via the SQLite queue (no RCON). See
[npc-admin.md](../plugins/npc-admin.md).
- `GET /api/npcs` — list NPCs from SQLite.
- `POST /api/npcs` — queue a spawn (name, health, kit, hostile, damage, speed,
  detect radius, respawn, …).
- `PATCH /api/npcs/:id` — queue a single-field update.
- `DELETE /api/npcs/:id` — queue removal of one NPC.
- `DELETE /api/npcs` — queue removal of all NPCs.
- `GET /api/npcs/commands/:id` — poll a queued command's status.

## Stack Sizes — `GET /config/stacksize`
Edits the StackSizeController plugin config directly on the shared volume
(`/rust-data/oxide/config/StackSizeController.json`): global multiplier, per-category,
and per-item multipliers.
- `POST /api/config/stacksize/save` — rebuild config from the form, write it, then
  `oxide.reload StackSizeController`.

## Server Settings — `GET /server/settings`
Structured server config form (name/identity, branding, seed/world size/max players,
ports, tickrate, save interval, gameplay toggles, idle kick, GSLT, server mode,
update-on-start, uMod enabled). `ServerSettings` type lives in `views/settings.ts`.
- `POST /api/server/settings/save` — write `/cfg/server-settings.json` **and** rewrite
  a managed block in `/cfg/server.cfg` (strips prior managed keys, appends branding +
  gameplay + mode-specific lines: `vanilla | pve | softcore | creative`). Takes effect
  on next server restart (see [game-server.md](../infrastructure/game-server.md)).

## Config Files — `GET /configs`, `GET /configs/:filename`
Browse, view, create, edit, and delete raw files in `/cfg` (filenames sanitized with
`basename`).
- `POST /api/configs/create | :filename/save | :filename/delete`.
- `POST /api/configs/reload` — `server.readcfg` via RCON.

## uMod Plugins — `GET /plugins`
Edit the external plugin list and trigger (re)installs. See
[umod-oxide.md](../plugins/umod-oxide.md).
- `POST /api/plugins/umod/save` — rewrite `plugins/umod-plugins.txt`.
- `POST /api/plugins/umod/reinstall` — run `install-plugins.sh` then `oxide.reload *`.

## Logs
- `GET /server-logs` + `GET /api/server-logs?tail=` — RustDedicated container logs.
- `GET /web-logs` + `GET /api/web-logs?tail=&level=&category=` — in-memory web log
  (`logger.ts`), filterable by level/category.
