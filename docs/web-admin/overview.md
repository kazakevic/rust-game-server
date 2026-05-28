# Web Admin — Overview

The admin dashboard is a Bun + Elysia + TypeScript app that renders server-side HTML
and controls the game server through Docker, RCON, and shared files. Source lives in
`web/`.

## Stack & layout

- **Runtime**: Bun (`oven/bun:1`). Use `bun` for all commands — never npm/npx/node.
- **HTTP**: Elysia (`web/src/index.ts` defines every route).
- **UI**: server-rendered template strings + Tailwind via the browser CDN. No client
  framework, no build step. See [design-system.md](design-system.md).
- **Dependencies**: `elysia`, `dockerode` (+ `@types/dockerode`, `bun-types`).

```
web/src/
├── index.ts        # Elysia app: all routes + API endpoints
├── auth.ts         # credential check + in-memory sessions
├── docker.ts       # dockerode wrapper (status/stats/logs/exec/control)
├── rcon.ts         # WebSocket RCON client
├── npc-db.ts       # bun:sqlite NPC command queue
├── logger.ts       # in-memory ring-buffer web log
└── views/          # one module per page + shared layout/components
```

See [backend.md](backend.md) for the modules and [features.md](features.md) for the
pages and API surface.

## Request lifecycle

1. Every protected route calls `authGuard(headers)`, which reads the `session` cookie
   and validates it via `auth.ts`. On failure: HTML routes 302 → `/login`; API routes
   return `{ error: "unauthorized" }`.
2. Page routes return server-rendered HTML (`Content-Type: text/html`) built from a
   `views/*` module wrapped in the shared `layout()`.
3. Mutating actions are `POST`/`PATCH`/`DELETE` endpoints under `/api/*` that talk to
   Docker / RCON / files and usually 302-redirect back to the originating page (the UI
   is plain HTML forms + light `fetch`).
4. Notable actions are recorded to the in-memory web log (`logger.ts`), viewable on the
   Web Logs page.

## Authentication

Single admin user from `ADMIN_USER`/`ADMIN_PASS` env. Login issues a random UUID
session token stored in-memory with a 24h TTL, set as an `HttpOnly; SameSite=Strict`
cookie. Sessions reset on web-app restart (no persistence, no CSRF tokens) — adequate
for a single trusted admin only.

## How it reaches the game server

- **Docker socket** → start/stop/restart, logs, stats, `exec` scripts (`docker.ts`).
- **RCON WebSocket** (`:28016`) → live game commands (`rcon.ts`).
- **Shared volume** → Oxide config + NPC SQLite at `/rust-data/...`.
- **`/cfg`** → write `server-settings.json` and `server.cfg`.

See [Architecture Overview](../architecture.md) for the full picture.
