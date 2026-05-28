# Documentation Index

Architecture and feature docs for **rust-gg** — a Dockerized vanilla Rust dedicated
server with a Bun/Elysia web admin dashboard. Start with the architecture overview,
then drill into the relevant area.

> **Maintenance rule:** after implementing a feature, add a short doc (or update the
> closest existing one) and add a one-line headline + path here in INDEX.md.

## Overview

- **[Architecture Overview](architecture.md)** — The two-service stack, how they share
  state (Docker socket, RCON, shared volume, `/cfg`), the NPC control loop, and the
  tech stack. Read this first.

## Infrastructure

- **[Docker & Compose](infrastructure/docker.md)** — Services, Dockerfiles, ports,
  shared `rust-data` volume and `./cfg`, dev override.
- **[Deployment, CI/CD, Makefile & Configuration](infrastructure/deployment.md)** —
  `.env` config, all `make` commands, the CI/CD pipeline (GitHub → Dokploy on a VPS
  building from `compose.yaml`), first-boot flow, security.
- **[Rust Game Server (entrypoint & boot)](infrastructure/game-server.md)** —
  `entrypoint.sh` boot sequence, SteamCMD update, settings/cfg loading, server identity.
- **[Reference: Rust Dedicated Server on Linux](infrastructure/rust-server-reference.md)**
  — Upstream Facepunch wiki notes (SteamCMD, AppID 258550, launch flags, ports,
  `server.cfg`) and how this repo automates each step.

## Plugins (Oxide/uMod)

- **[uMod / Oxide & Plugin Installation](plugins/umod-oxide.md)** — Framework + plugin
  install/update scripts (`install-umod.sh`, `install-plugins.sh`, `update-umod.sh`,
  `setup-cron.sh`), `umod-plugins.txt`, version-aware idempotency.
- **[NPC System (NpcAdmin.cs)](plugins/npc-admin.md)** — RCON-less NPC control via a
  shared SQLite command queue: plugin timers, DB schema, command protocol.

## Web Admin Dashboard

- **[Overview](web-admin/overview.md)** — Stack, file layout, request lifecycle, auth,
  and how the app reaches the game server.
- **[Backend Modules](web-admin/backend.md)** — `index.ts`, `auth.ts`, `docker.ts`,
  `rcon.ts`, `npc-db.ts`, `logger.ts`.
- **[Design System & Views](web-admin/design-system.md)** — `layout.ts`,
  `components.ts`, the page modules, and UI conventions.
- **[Features & Routes](web-admin/features.md)** — Every page and `/api/*` endpoint:
  dashboard, RCON, players, NPCs, stack sizes, settings, config files, plugins, logs.
