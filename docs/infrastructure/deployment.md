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

## CI/CD & Production (Dokploy)

There is **no separate CI pipeline** (no GitHub Actions, GitLab CI, etc.). The whole
delivery flow is a single VPS running **Docker** + **[Dokploy](https://dokploy.com/)**,
and `compose.yaml` is the single source of truth for what ships.

### Pipeline

```
git push  →  GitHub (kazakevic/rust-game-server)  →  Dokploy on the VPS
                                                         │
                                          reads compose.yaml, builds both images:
                                          • rust-server  ← ./Dockerfile
                                          • web-admin    ← ./web/Dockerfile
                                                         │
                                          docker compose up -d (recreates changed services)
```

1. Push to the GitHub repo (the production branch).
2. Dokploy is configured as a **Compose** application pointing at this repo and
   `compose.yaml`. On a new commit it pulls, then builds and (re)deploys.
3. Both services are built from source on the VPS via their `build:` contexts
   (`.` and `./web`) — there is no image registry in the loop.
4. Changed services are recreated; the named `rust-data` volume and `./cfg` persist
   across deploys, so server identity, map, plugins, and saved settings survive.

> The Rust game server pins `platform: linux/amd64` (SteamCMD/RustDedicated is x86-64
> only) and runs with `seccomp:unconfined` — both must be preserved for the build to
> boot on the VPS. See the comments in `compose.yaml`.

### Keeping `compose.yaml` Dokploy-compatible

Because Dokploy deploys straight from `compose.yaml`, every prod-affecting change must
land there (not only in the Makefile, which is local-only):

- Plain Compose, named volumes, `.env`-driven config.
- No host-specific paths beyond the Docker socket mount (`/var/run/docker.sock`) and
  the repo-relative bind mounts (`./plugins`, `./cfg`).
- Set production secrets/config via Dokploy's environment (it provides the `.env` the
  services load) — never commit `.env`.
- Avoid changes that only work through the local `make` flow or `compose.dev.yaml`
  (the dev override is **not** used in prod).

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
