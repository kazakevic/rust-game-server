# Web Admin — Backend Modules

The non-view TypeScript modules under `web/src/`. Each is small and single-purpose.

## `index.ts` — Elysia app

Wires every route. Holds one shared `RconClient` instance. Helpers:
- `getCookie(headers, name)` — parse a cookie value.
- `authGuard(headers)` — returns a 302-to-`/login` `Response` when unauthenticated
  (or it's used to gate API routes), else `null`. Called at the top of every protected
  handler.

Routes fall into **page routes** (return HTML) and **`/api/*` action routes** (perform
an effect, usually redirect back). The full route + endpoint list is in
[features.md](features.md).

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
- `restartServer()` / `stopServer()` / `startServer()` — lifecycle control.
- `execInServer(cmd[])` — run a command in the container; manually de-frames Docker's
  8-byte multiplexed stream headers to return clean stdout.
- `getServerLogs(tail, since?)` — fetch container logs (same de-framing).

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
