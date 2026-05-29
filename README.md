# rust-gg

A Dockerized **vanilla Rust dedicated server** built for streamers, with anti-stream-sniper protection and a web-based admin dashboard for real-time server management.

- Vanilla gameplay — no custom game modes or progression mods
- Anti-stream-sniper protection to keep streamers safe from viewers tracking them
- NPC support — spawn and configure AI enemies via the web dashboard
- Web admin dashboard — server controls, RCON, logs, config, and plugin management

---

## Stack

| Layer | Technology |
|---|---|
| Game Server | Rust Dedicated Server (SteamCMD) |
| Modding | Oxide/uMod (C# plugins) |
| Web Dashboard | Bun.js + Elysia + TypeScript |
| Styling | Tailwind CSS |
| Database | SQLite (plugin data + NPC queue) |
| Deployment | Docker Compose |

---

## Project Structure

```
rust-gg/
├── plugins/           # Custom Oxide/uMod C# plugins
│   ├── NpcAdmin.cs        # Web-driven NPC manager
│   └── umod-plugins.txt   # External uMod plugins to auto-install
├── web/               # Admin dashboard (Bun + TypeScript)
├── cfg/               # Seed config (baked to image; runtime /cfg is the rust-cfg volume)
├── scripts/           # uMod/plugin install scripts
├── Dockerfile         # Rust server image
├── compose.yaml       # Docker Compose (rust-server + web-admin)
└── entrypoint.sh      # Server startup script
```

---

## Quick Start

**1. Copy the example env file and fill in your values:**

```bash
cp .env.example .env
```

Edit `.env` — at minimum change the passwords:

```env
RUST_RCON_PASSWORD=your_strong_password
ADMIN_PASS=your_admin_password
```

**2. Build and start:**

```bash
make build
make start
```

The Rust server will download via SteamCMD on first boot (this takes a few minutes). uMod and plugins install automatically.

**3. Open the admin dashboard:**

```
http://localhost:3000
```

Login with the `ADMIN_USER` / `ADMIN_PASS` from your `.env`.

---

## Configuration

All configuration lives in `.env`. Key variables:

```env
# Server
RUST_SERVER_NAME=My Rust Server
RUST_SERVER_SEED=12345          # Map seed
RUST_SERVER_WORLDSIZE=3500      # Map size
RUST_SERVER_MAXPLAYERS=100
RUST_SERVER_PORT=28015
RUST_RCON_PORT=28016
RUST_RCON_PASSWORD=changeme     # Change this

# Updates
RUST_UPDATE_ON_START=1          # Set to 0 to skip SteamCMD update on restart
UMOD_ENABLED=1

# Web Dashboard
WEB_PORT=3000
ADMIN_USER=admin
ADMIN_PASS=changeme             # Change this
```

---

## Make Commands

| Command | Description |
|---|---|
| `make start` | Start all services |
| `make stop` | Stop all services |
| `make restart` | Restart all services |
| `make build` | Rebuild Docker images |
| `make clean` | Stop and remove volumes |
| `make logs` | Tail Rust server logs |
| `make reload` | Copy plugins to server (Oxide auto-reloads) |
| `make plugins` | Re-run plugin install script |
| `make update` | Stop, update server via SteamCMD, restart |
| `make update-umod` | Update Oxide/uMod in running container |
| `make web-logs` | Tail web dashboard logs |
| `make web-restart` | Rebuild and restart web dashboard only |
| `make dev` | Start web dashboard in dev mode (hot reload) |

---

## Web Dashboard

| Page | Features |
|---|---|
| Dashboard | Server status, CPU/RAM, player count, plugin reload, time/weather controls |
| RCON | Live command terminal with history |
| NPC Manager | Spawn, configure, and remove AI NPCs |
| Stack Sizes | Global and per-item stack multiplier config |
| Server Settings | Hostname, seed, world size, max players, game mode |
| Config Browser | Edit raw `.cfg` files |
| Logs | Server console output |

---

## Plugins

### Custom Plugins

| Plugin | Description |
|---|---|
| `NpcAdmin.cs` | SQLite-based NPC control (web → plugin queue) |

### External Plugins (auto-installed)

Listed in `plugins/umod-plugins.txt` — includes Kits, GatherManager, HumanNPC, NTeleportation, StackSizeController, and more.

---

## NPC System

NPCs are managed through the web dashboard without RCON. The web app writes commands to a shared SQLite database; `NpcAdmin.cs` polls it every 2 seconds and executes them. NPC positions sync back to the database periodically.

Supported attributes: name, health, kit, damage, speed, detection radius, hostile/friendly, invulnerable, lootable, auto-respawn.

---

## Security Notes

- `.env` is gitignored — never commit it
- Change `RUST_RCON_PASSWORD` and `ADMIN_PASS` before exposing the server publicly
- Admin sessions expire after 24 hours
- The web dashboard is intended for trusted admin use only — do not expose it to the public internet without additional hardening
