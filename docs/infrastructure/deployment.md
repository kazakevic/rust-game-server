# Deployment, Makefile & Configuration

How to build, run, and configure the stack — locally and in production.

## Configuration (`.env`)

All config lives in `.env` (gitignored; copy from `.env.example`). Both Compose
services load it via `env_file`. Key groups:

- **Game server** — `RUST_SERVER_NAME`, `RUST_SERVER_IDENTITY`, `RUST_SERVER_SEED`,
  `RUST_SERVER_WORLDSIZE`, `RUST_SERVER_MAXPLAYERS`, ports
  (`RUST_SERVER_PORT`, `RUST_SERVER_QUERYPORT`, `RUST_RCON_PORT`, `RUST_APP_PORT`),
  `RUST_RCON_PASSWORD`, `RUST_RCON_WEB`.
- **Lifecycle** — `RUST_UPDATE_ON_START` (set `0` to skip SteamCMD update on
  restart), `UMOD_ENABLED`.
- **Web dashboard** — `WEB_PORT`, `ADMIN_USER`, `ADMIN_PASS`.

> The web dashboard's Server Settings page can override most of these by writing
> `cfg/server-settings.json`, which the entrypoint reads ahead of env defaults. See
> [game-server.md](game-server.md).

## Make commands

| Command | Description |
|---|---|
| `make build` | Build both Docker images |
| `make up` / `make down` | Start / stop all services |
| `make restart` | Restart all services |
| `make clean` | Stop and remove volumes (destroys server data) |
| `make logs` | Tail Rust server logs |
| `make shell` / `make rcon` | Bash into the `rust-server` container |
| `make update` | Down, then up with `RUST_UPDATE_ON_START=1` (SteamCMD update) |
| `make plugins` / `make reload` | Run `install-plugins.sh` in the container |
| `make update-umod` | Run `update-umod.sh` in the running container |
| `make web-logs` | Tail web dashboard logs |
| `make web-restart` | Rebuild + restart only `web-admin` |
| `make dev` | Start `web-admin` in watch/hot-reload mode (dev override) |

## Production (Dokploy)

Production deploys via **Dokploy** on a VPS. Keep `compose.yaml` Dokploy-compatible:
plain Compose, named volumes, `.env`-driven config, no host-specific paths beyond the
Docker socket mount. Avoid changes that only work with the local Makefile flow.

## First boot

On first `make up`, `rust-server` downloads RustDedicated via SteamCMD (several
minutes), then installs uMod and any plugins listed in `plugins/umod-plugins.txt`.
The dashboard is then reachable at `http://localhost:${WEB_PORT}` (default 3000);
log in with `ADMIN_USER` / `ADMIN_PASS`.

## Security notes

- Never commit `.env`; rotate `RUST_RCON_PASSWORD` and `ADMIN_PASS` before exposing
  the server publicly.
- Admin sessions expire after 24h (in-memory; see [backend.md](../web-admin/backend.md)).
- The dashboard is for trusted admins only — do not expose it to the public internet
  without additional hardening (it has no CSRF protection and stores credentials in
  plain env vars).
