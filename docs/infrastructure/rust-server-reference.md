# Reference: Rust Dedicated Server on Linux

External reference distilled from the official Facepunch wiki:
<https://wiki.facepunch.com/rust/Creating-a-server>

This is the **upstream, manual** way to run a Rust dedicated server on Linux. This repo
automates the same steps inside Docker — see the mapping at the bottom and
[game-server.md](game-server.md).

## SteamCMD

The server is downloaded with SteamCMD (no Steam account needed; anonymous login):

```bash
wget https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
tar xvfz steamcmd_linux.tar.gz
chmod +x steamcmd.sh
```

## Install / update the server

**App ID: `258550`.** Re-run the same command to update; `validate` repairs files:

```bash
./steamcmd.sh +force_install_dir ../rust +login anonymous +app_update 258550 validate +quit
```

## Launch flags

Started via the `RustDedicated` binary with `+convar value` flags. Key ones:

| Flag | Purpose | Typical |
|---|---|---|
| `-batchmode` | Run headless, no GUI | required |
| `+server.port` | Game connection port (UDP) | 28015 |
| `+server.queryport` | Server-browser discovery (UDP); **must differ** from `server.port` | port+1 |
| `+server.identity` | Internal server/save name | `"server1"` |
| `+server.hostname` | Display name in the browser | `"My Server"` |
| `+server.seed` | Map seed (0–2147483647) | 1234 |
| `+server.worldsize` | Map size (1000–6000) | 4000 |
| `+server.maxplayers` | Player cap | 10 |
| `+rcon.port` | Admin RCON port (TCP) | 28016 |
| `+rcon.password` | RCON password (change it) | — |
| `+rcon.web` | Enable WebSocket RCON | 1 |

Example:

```bash
RustDedicated -batchmode +server.port 28015 +server.identity "server1" \
  +server.hostname "Name of Server" +server.seed 1234 +server.worldsize 4000 \
  +server.maxplayers 10 +rcon.port 28016 +rcon.password letmein +rcon.web 1
```

## Ports

- `server.port` (UDP) — player connections.
- `server.queryport` (UDP) — server-browser discovery; defaults to
  `max(server.port, rcon.port) + 1` and **cannot equal** `server.port`.
- `rcon.port` (TCP) — administrative/RCON access.

Both `server.port` and `server.queryport` must be open and reachable.

## Config files

Persistent config lives at `rust/server/<server.identity>/cfg/server.cfg`. Entries use
**no `+`/`-` prefix**, e.g.:

```
server.maxplayers 10
server.hostname "Tom Server"
```

The wiki notes the server reads `server.cfg` at startup. Look for
`"Server startup complete"` in the logs to confirm a successful boot.

## Linux console / RCON

The Linux console is **not interactive** like Windows — you need an RCON client to send
commands. (This repo's web dashboard is exactly that client; see
[../web-admin/backend.md](../web-admin/backend.md).) Manual hosts often run the server
under `screen` for background execution.

## How this repo automates it

| Wiki step | Where it lives here |
|---|---|
| Install SteamCMD | `cm2network/steamcmd` base image (`Dockerfile`) |
| Linux libraries | `Dockerfile` installs `lib32gcc-s1`, `libgdiplus`, etc. |
| `app_update 258550 validate` | `entrypoint.sh` (gated by `RUST_UPDATE_ON_START`) |
| Launch flags | built in `entrypoint.sh` from `.env` + `server-settings.json` |
| `server.cfg` / identity | `cfg/` dir → copied to `server/<identity>/cfg/` on boot |
| `screen` / background | the container itself + `restart: unless-stopped` |
| RCON client | the Bun/Elysia web dashboard over web RCON (`rcon.web 1`) |

See [game-server.md](game-server.md) for the exact boot sequence and
[deployment.md](deployment.md) for the env/Make workflow.
