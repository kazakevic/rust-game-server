# Docker & Compose

Defines the two-service stack and how they share state.

## Files

- `compose.yaml` — production/base compose (both services)
- `compose.dev.yaml` — dev override for the web app (source bind-mount + `--watch`)
- `Dockerfile` — `rust-server` image (SteamCMD base + Mono/libs)
- `web/Dockerfile` — `web-admin` image (`oven/bun:1`)
- `.dockerignore`, `web/.dockerignore`

## Services

### `rust-server`
- Built from the root `Dockerfile`, pinned `platform: linux/amd64`.
- `security_opt: seccomp:unconfined` — steamcmd's bundled Steam runtime segfaults
  under Docker's default seccomp filter on some VPS kernels (`futex robust_list`
  loop). Disabling seccomp for this container resolves it.
- Ports exposed (env-overridable): game `28015/udp+tcp`, RCON `28016/tcp`,
  query `28017/udp`, companion app `28082/tcp`.
- Volumes: `rust-data:/rust` (the install), `./plugins:/plugins:ro`,
  `rust-cfg:/cfg` (config; named volume, writable so the entrypoint can seed defaults).
- `stop_grace_period: 5m` — Rust saves the world on SIGTERM; the default 10s is too
  short for a large map. The web admin passes a matching per-call timeout
  (`RUST_STOP_TIMEOUT`, default 300s) when it stops/restarts via the Docker socket.
- `stdin_open` + `tty` so the RustDedicated console attaches.

### `web-admin`
- Built from `web/Dockerfile`.
- Port `WEB_PORT` (default `3000`).
- `RUST_CONTAINER_NAME=rust-server` so dockerode can find the game container.
- Volumes: `/var/run/docker.sock:ro` (control plane), `rust-cfg:/cfg` (read-write —
  writes settings/cfg), `rust-data:/rust-data` (read Oxide config + NPC SQLite).
- `depends_on: rust-server`.

## Shared state

The `rust-data` named volume is the key coupling: mounted at `/rust` in the game
server and `/rust-data` in the web app. This is how the web app reaches
`oxide/config/StackSizeController.json` and `oxide/data/NpcAdmin.db` without RCON.

`rust-cfg` is a second named volume mounted at `/cfg` in both services so dashboard
settings reach the boot sequence **and survive Dokploy redeploys**. It is *not* a
repo-relative bind mount — Dokploy refreshes the git checkout on each deploy, which would
drop runtime-written files like `server-settings.json` (the old GSLT-resets-on-deploy
bug). Repo defaults (`users.cfg`) are baked into the `rust-server` image at `/seed-cfg`
and copied into the volume on first boot (copy-if-not-exists; see
[game-server.md](game-server.md)).

## Dockerfiles

- **`Dockerfile`** — `FROM cm2network/steamcmd:latest`, installs `lib32gcc-s1`,
  `libgdiplus`, `curl`, `unzip`, `jq`; copies `entrypoint.sh` + `scripts/` and bakes
  `cfg/` defaults to `/seed-cfg`; creates a steam-owned `/cfg` so the fresh `rust-cfg`
  volume inherits writable ownership; runs as the `steam` user;
  `ENTRYPOINT ["/entrypoint.sh"]`.
- **`web/Dockerfile`** — `FROM oven/bun:1`, `bun install --frozen-lockfile`, runs
  `bun run src/index.ts`.

## Dev mode

`make dev` layers `compose.dev.yaml` over the base file, bind-mounting `web/src` and
running Bun with `--watch` for hot reload. See [deployment.md](deployment.md).
