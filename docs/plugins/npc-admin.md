# NPC System (NpcAdmin.cs)

Web-driven NPC management with **no RCON involved**. The web app and the Oxide plugin
communicate purely through a shared SQLite database in the `rust-data` volume.

> Status: `NpcAdmin.cs` currently lives in `plugins/disabled/` (shelved). The web app's
> NPC page and `npc-db.ts` remain and will drive the plugin once it is re-enabled by
> moving the file back into `plugins/`.

## The plugin (`NpcAdmin.cs`)

Oxide `RustPlugin` (`[Info("NpcAdmin", "rust-gg", "4.0.0")]`) with `[PluginReference]`
dependencies on **HumanNPC** (spawning) and **Kits** (loadouts). On
`OnServerInitialized` it opens `NpcAdmin.db` and starts two timers:

- **Poll timer (every 2s)** — `PollCommands()` reads up to 5 `pending` rows from
  `npc_commands`, marks them `processing`, dispatches via `ProcessCommand`, then writes
  back `done`/`failed` + a `result`.
- **Position timer (every 10s)** — `UpdatePositions()` writes each live NPC's
  `pos_x/y/z` back to `spawned_npcs`, marks missing ones `dead`, and garbage-collects
  finished `npc_commands` older than 1 hour.

It also hooks `OnEntityTakeDamage` to zero damage for NPCs flagged invulnerable.

## Database

`/rust-data/oxide/data/NpcAdmin.db` (WAL mode). Two tables, created by **both** sides
so either can start first (`npc-db.ts` mirrors the schema):

- **`spawned_npcs`** — current NPC state: `npc_id`, `name`, `health`, `kit`,
  `hostile`, `invulnerable`, `lootable`, `damage`, `speed`, `detect_radius`,
  `respawn`, `respawn_delay`, position, `status` (`pending`/`alive`/`dead`/`removed`).
- **`npc_commands`** — the command queue: `id`, `action`, JSON `payload`, `status`
  (`pending`→`processing`→`done`/`failed`), `result`, timestamps.

## Command protocol

The web app enqueues a row; the plugin consumes it. Actions (`ProcessCommand` switch):

| `action` | Payload | Effect |
|---|---|---|
| `spawn` | full NPC params (name, health, kit, hostile, …) | Spawn a HumanNPC, apply kit + attributes, insert into `spawned_npcs` |
| `remove` | `{ npcId }` | Kill the NPC, mark row `removed` |
| `remove_all` | `{}` | Remove every non-`removed` NPC |
| `update` | `{ npcId, field, value }` | Patch one attribute (`health`, `kit`, `invulnerable`, `hostile`, `lootable`, `damage`, `speed`, `detect_radius`) on the live NPC + row |

The web app polls `npc_commands.status` by id to surface success/failure in the UI.

## Web side

`web/src/npc-db.ts` opens the same SQLite file with `bun:sqlite` and exposes
`getNpcs()`, `queueSpawn()`, `queueRemove()`, `queueRemoveAll()`, `queueUpdate()`,
`getCommandStatus()`. The NPC Manager page and `/api/npcs*` routes wrap these — see
[backend.md](../web-admin/backend.md) and [features.md](../web-admin/features.md).
