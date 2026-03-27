import { Database } from "bun:sqlite";

const DB_PATH = "/rust-data/oxide/data/NpcAdmin.db";

let db: Database | null = null;

function getDb(): Database {
  if (!db) {
    db = new Database(DB_PATH, { create: true });
    db.exec("PRAGMA journal_mode=WAL;");

    // Ensure tables exist (web might start before plugin)
    db.exec(`
      CREATE TABLE IF NOT EXISTS spawned_npcs (
        npc_id TEXT PRIMARY KEY,
        name TEXT NOT NULL DEFAULT 'NPC',
        health REAL NOT NULL DEFAULT 100,
        kit TEXT,
        hostile INTEGER NOT NULL DEFAULT 0,
        invulnerable INTEGER NOT NULL DEFAULT 0,
        lootable INTEGER NOT NULL DEFAULT 1,
        damage REAL NOT NULL DEFAULT 10,
        speed REAL NOT NULL DEFAULT 3,
        detect_radius REAL NOT NULL DEFAULT 30,
        respawn INTEGER NOT NULL DEFAULT 0,
        respawn_delay INTEGER NOT NULL DEFAULT 60,
        pos_x REAL,
        pos_y REAL,
        pos_z REAL,
        status TEXT NOT NULL DEFAULT 'pending',
        created_at TEXT NOT NULL DEFAULT (datetime('now'))
      );
    `);

    db.exec(`
      CREATE TABLE IF NOT EXISTS npc_commands (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        action TEXT NOT NULL,
        payload TEXT NOT NULL DEFAULT '{}',
        status TEXT NOT NULL DEFAULT 'pending',
        result TEXT,
        created_at TEXT NOT NULL DEFAULT (datetime('now')),
        processed_at TEXT
      );
    `);
  }
  return db;
}

export interface NpcRecord {
  npc_id: string;
  name: string;
  health: number;
  kit: string | null;
  hostile: number;
  invulnerable: number;
  lootable: number;
  damage: number;
  speed: number;
  detect_radius: number;
  respawn: number;
  respawn_delay: number;
  pos_x: number | null;
  pos_y: number | null;
  pos_z: number | null;
  status: string;
  created_at: string;
}

export interface CommandRecord {
  id: number;
  action: string;
  payload: string;
  status: string;
  result: string | null;
  created_at: string;
  processed_at: string | null;
}

export interface SpawnParams {
  steamId: string;
  name: string;
  health: number;
  kit?: string;
  hostile: boolean;
  invulnerable: boolean;
  lootable: boolean;
  damage: number;
  speed: number;
  detectRadius: number;
  respawn: boolean;
  respawnDelay: number;
}

export function getNpcs(): NpcRecord[] {
  return getDb()
    .query("SELECT * FROM spawned_npcs WHERE status != 'removed' ORDER BY created_at DESC;")
    .all() as NpcRecord[];
}

export function queueSpawn(params: SpawnParams): number {
  const stmt = getDb().prepare(
    "INSERT INTO npc_commands (action, payload) VALUES ('spawn', ?);"
  );
  const result = stmt.run(JSON.stringify(params));
  return Number(result.lastInsertRowid);
}

export function queueRemove(npcId: string): number {
  const stmt = getDb().prepare(
    "INSERT INTO npc_commands (action, payload) VALUES ('remove', ?);"
  );
  const result = stmt.run(JSON.stringify({ npcId }));
  return Number(result.lastInsertRowid);
}

export function queueRemoveAll(): number {
  const stmt = getDb().prepare(
    "INSERT INTO npc_commands (action, payload) VALUES ('remove_all', '{}');"
  );
  const result = stmt.run();
  return Number(result.lastInsertRowid);
}

export function queueUpdate(npcId: string, field: string, value: any): number {
  const stmt = getDb().prepare(
    "INSERT INTO npc_commands (action, payload) VALUES ('update', ?);"
  );
  const result = stmt.run(JSON.stringify({ npcId, field, value }));
  return Number(result.lastInsertRowid);
}

export function getCommandStatus(cmdId: number): CommandRecord | null {
  return getDb()
    .query("SELECT * FROM npc_commands WHERE id = ?;")
    .get(cmdId) as CommandRecord | null;
}
