# Architecture Overview

rust-gg is a Dockerized **vanilla Rust dedicated server** for streamers, paired with
a **web admin dashboard** for real-time management. The whole stack runs as two
Docker Compose services that share state through a Docker volume and the Docker socket.

## Services

| Service | Image | Role |
|---|---|---|
| `rust-server` | Built from root `Dockerfile` (SteamCMD base) | Runs RustDedicated, installs uMod/Oxide and plugins on boot |
| `web-admin` | Built from `web/Dockerfile` (Bun) | Elysia web app for server control, RCON, configs, plugins, NPCs |

## How the pieces talk

```
                 browser
                    │ HTTP (cookie session)
                    ▼
            ┌──────────────┐
            │  web-admin   │  Bun + Elysia
            │  (port 3000) │
            └──────┬───────┘
       ┌───────────┼─────────────┬──────────────────┐
       │           │             │                  │
   Docker socket   RCON ws    shared volume     shared cfg volume
   (start/stop/    (game       rust-data/        rust-cfg
    logs/stats/    commands)   oxide/...         (server.cfg,
    exec)          :28016      (SQLite,          server-settings.json)
       │                       StackSize cfg)
       ▼
 ┌──────────────┐
 │ rust-server  │  RustDedicated + Oxide/uMod
 │  (:28015 …)  │  NpcAdmin.cs polls SQLite queue
 └──────────────┘
```

Four integration channels:

1. **Docker socket** — `web-admin` mounts `/var/run/docker.sock` (read-only) and uses
   `dockerode` to inspect, start/stop/restart, stream logs, read stats, and `exec`
   scripts inside `rust-server`. See [Web Backend](web-admin/backend.md).
2. **RCON over WebSocket** — `web-admin` connects to RustDedicated's web RCON on
   `:28016` to run live game commands (status, env.time, oxide.reload, teleport, …).
3. **Shared `rust-data` volume** — both services mount the Rust install. The web app
   reads/writes Oxide config (`StackSizeController.json`) and the NPC SQLite database
   directly on disk.
4. **Shared `rust-cfg` volume** (`/cfg`) — the web app writes `server-settings.json` and
   `server.cfg`; the game server's [entrypoint](infrastructure/game-server.md) reads them
   on boot. A named volume (not a repo bind mount) so settings survive Dokploy redeploys;
   seeded from image defaults on first boot.

## The NPC control loop (RCON-less IPC)

NPCs are managed without RCON. The web app writes commands to a SQLite database in the
shared volume; the `NpcAdmin.cs` Oxide plugin polls that database every 2 seconds and
executes them, syncing NPC positions back periodically. See
[NPC System](plugins/npc-admin.md).

## Tech stack

| Layer | Technology |
|---|---|
| Game server | RustDedicated via SteamCMD (AppID 258550) |
| Modding | Oxide/uMod, C# plugins |
| Web backend | Bun + Elysia + TypeScript |
| Web UI | Server-rendered HTML + Tailwind (browser CDN) |
| Persistence | SQLite (NPC queue), JSON/cfg files on shared volume |
| Orchestration | Docker Compose; prod via Dokploy on a VPS |

## Where to look next

- Infrastructure & deploy: [docker.md](infrastructure/docker.md),
  [deployment.md](infrastructure/deployment.md),
  [game-server.md](infrastructure/game-server.md)
- Plugins: [umod-oxide.md](plugins/umod-oxide.md), [npc-admin.md](plugins/npc-admin.md)
- Web app: [overview.md](web-admin/overview.md), [backend.md](web-admin/backend.md),
  [design-system.md](web-admin/design-system.md), [features.md](web-admin/features.md)
