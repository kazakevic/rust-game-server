# Web Admin — Backend Modules

The non-view TypeScript modules under `web/src/`. Each is small and single-purpose.

## `index.ts` — Elysia app

Wires every route. Holds one shared `RconClient` instance. Helpers:
- `getCookie(headers, name)` — parse a cookie value.
- `authGuard(headers)` — returns a 302-to-`/login` `Response` when unauthenticated, else
  `null`. Used by **page** routes.
- `apiUnauthorized(headers)` — JSON-friendly guard for fetch-driven `/api/*` endpoints:
  returns `{ error: "unauthorized" }` (not a redirect) when the session is invalid.
- `getDashboardState()` — gathers `{ status, stats, serverInfo }` (container inspect +
  stats + a live `serverinfo` RCON call). Shared by `GET /dashboard` (initial render) and
  `GET /api/server/status` (the dashboard's live poll).
- `runServerAction(label, fn)` — fires a long-running container op **without blocking**
  the HTTP response; failures land in the web log. Used by the start/stop/restart/wipe
  endpoints so the browser never hangs while Rust saves the world.

Routes fall into **page routes** (return HTML) and **`/api/*` action routes**. Mutating
control endpoints now return JSON immediately and let the client poll for the result; the
full route + endpoint list is in [features.md](features.md).

## `auth.ts` — sessions

- `validateCredentials(user, pass)` — compares against `ADMIN_USER`/`ADMIN_PASS` env.
- `generateSession()` — `crypto.randomUUID()` token stored in an in-memory `Map` with
  a creation timestamp.
- `validateSession(token)` — checks existence + 24h expiry (evicts on expiry).
- `destroySession(token)` — logout.

In-memory only: restart clears all sessions.

## `docker.ts` — container control (dockerode)

Connects to `/var/run/docker.sock` and targets `RUST_CONTAINER_NAME`.
- `getServerStatus()` — running flag, status, `startedAt`, health (via `inspect`).
- `getServerStats()` — CPU % (computed from cpu/precpu deltas) and memory usage from a
  one-shot `stats` sample.
- `restartServer()` / `stopServer()` / `startServer()` — lifecycle control. `stop`/
  `restart` pass a grace timeout (`RUST_STOP_TIMEOUT`, default `300`s) to Docker so Rust
  finishes saving before SIGKILL (mirrors `stop_grace_period` in `compose.yaml`).
- `execInServer(cmd[])` — run a command in the container; manually de-frames Docker's
  8-byte multiplexed stream headers to return clean stdout.
- `checkForUpdate()` — execs `scripts/update-check.sh` in the game-server container and
  parses its JSON to return `{ installed, latest, branch, updateAvailable }` (build-id
  compare; requires the container running). Powers the dashboard's Check-for-updates button.
- `getServerLogs(tail, since?)` — fetch container logs (same de-framing).

## `update-scheduler.ts` — periodic update checker

Opt-in background loop (enabled with `RUST_UPDATE_CHECK_ENABLED=1`), started from
`index.ts` after `listen`. Every `RUST_UPDATE_CHECK_INTERVAL` minutes it calls
`checkForUpdate()`; when a new build is found it broadcasts RCON `say` warnings on a
countdown (`RUST_UPDATE_RESTART_DELAY` minutes, checkpoints at 5m/3m/1m/30s/10s) then
`restartServer()` — the entrypoint downloads the build on boot.
- `startUpdateScheduler(rcon)` — begin the loop (no-op when disabled).
- `getSchedulerStatus()` — `{ enabled, intervalMin, delayMin, restartScheduled, restartAt,
  pendingBuild }`, surfaced via `/api/server/status` for the dashboard banner/countdown.
- `cancelScheduledRestart(reason)` — clear a pending restart; "snoozes" that build so it
  won't auto-reschedule until a newer one appears. Called by the Cancel button and by every
  manual lifecycle action.

## `a2s.ts` — query-port (A2S) check

Minimal A2S_INFO ("Source Engine Query") UDP client — the same query the in-game server
browser, BattleMetrics, and the Steam master server use to discover a server.
- `queryA2SInfo(host, port, timeoutMs=2500)` — sends A2S_INFO, handles the 0x41 challenge
  handshake (resends with the challenge), parses the 0x49 reply into
  `{ answering, name, map, players, maxPlayers }`. Never rejects: failures resolve to
  `{ answering: false, error }`.

Powers `GET /api/server/queryport` and the dashboard's **Server Visibility** panel. It runs
web-admin → `rust-server` over the Docker network, so it proves the *server* is answering;
it cannot detect an **external** firewall block (a packet to our own public IP loops back
without traversing the edge firewall). Therefore `answering: true` here while the server is
still missing from the browser ⇒ the query port is blocked by an external firewall.

## `rcon.ts` — `RconClient`

WebSocket client for RustDedicated web RCON (`ws://host:port/password`).
- Lazily connects/reuses one socket; 5s connect timeout.
- `command(cmd)` — sends `{ Identifier, Message, Name }`, correlates the response by
  incrementing `Identifier`, resolves the `Message`; 30s command timeout.
- Rejects all pending on close; `disconnect()` to tear down.

## `npc-db.ts` — NPC command queue (`bun:sqlite`)

Opens `/rust-data/oxide/data/NpcAdmin.db` (WAL), creating `spawned_npcs` +
`npc_commands` if absent (schema mirrors the plugin so either side can start first).
Exposes `getNpcs()`, `queueSpawn()`, `queueRemove()`, `queueRemoveAll()`,
`queueUpdate()`, `getCommandStatus()`. Protocol detail: [npc-admin.md](../plugins/npc-admin.md).

## `logger.ts` — in-memory web log

Ring buffer (max 1000 entries) of `{ timestamp, level, category, message }`.
- `info/warn/error(category, message)` — append.
- `getWebLogs({ tail, level, category })` — filtered tail.
- `getCategories()` — distinct categories for the filter UI.

Surfaced on the Web Logs page; not persisted across restarts.
