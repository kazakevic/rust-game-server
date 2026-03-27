using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NpcAdmin", "rust-gg", "3.0.0")]
    [Description("Web-driven NPC manager — reads commands from shared SQLite, no RCON needed")]
    public class NpcAdmin : RustPlugin
    {
        [PluginReference]
        private Plugin HumanNPC;

        [PluginReference]
        private Plugin Kits;

        private HashSet<ulong> invulnerableNpcs = new HashSet<ulong>();
        private HashSet<ulong> spawnedNpcs = new HashSet<ulong>();

        private readonly Core.SQLite.Libraries.SQLite _sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
        private Connection _db;

        private Timer _pollTimer;
        private Timer _posTimer;

        #region Hooks

        private void OnServerInitialized()
        {
            OpenDatabase();
            _pollTimer = timer.Every(2f, PollCommands);
            _posTimer = timer.Every(10f, UpdatePositions);
        }

        private void Unload()
        {
            _pollTimer?.Destroy();
            _posTimer?.Destroy();
            CloseDatabase();
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player == null) return null;
            if (!invulnerableNpcs.Contains(player.userID)) return null;

            info.damageTypes.ScaleAll(0f);
            return true;
        }

        #endregion

        #region Database

        private void OpenDatabase()
        {
            _db = _sqlite.OpenDb("NpcAdmin.db", this);
            if (_db == null)
            {
                PrintError("Failed to open SQLite database!");
                return;
            }

            _sqlite.ExecuteNonQuery(Sql.Builder.Append("PRAGMA journal_mode=WAL;"), _db);

            string createNpcs = @"
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
                );";

            string createCommands = @"
                CREATE TABLE IF NOT EXISTS npc_commands (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    action TEXT NOT NULL,
                    payload TEXT NOT NULL DEFAULT '{}',
                    status TEXT NOT NULL DEFAULT 'pending',
                    result TEXT,
                    created_at TEXT NOT NULL DEFAULT (datetime('now')),
                    processed_at TEXT
                );";

            _sqlite.ExecuteNonQuery(Sql.Builder.Append(createNpcs), _db);
            _sqlite.ExecuteNonQuery(Sql.Builder.Append(createCommands), _db);

            // Migrate: drop old spawned_npcs if schema doesn't match (missing columns)
            MigrateSchema();

            LoadNpcsFromDb();
        }

        private void MigrateSchema()
        {
            // Add missing columns if upgrading from v2
            string[] migrations = new string[]
            {
                "ALTER TABLE spawned_npcs ADD COLUMN lootable INTEGER NOT NULL DEFAULT 1;",
                "ALTER TABLE spawned_npcs ADD COLUMN damage REAL NOT NULL DEFAULT 10;",
                "ALTER TABLE spawned_npcs ADD COLUMN speed REAL NOT NULL DEFAULT 3;",
                "ALTER TABLE spawned_npcs ADD COLUMN detect_radius REAL NOT NULL DEFAULT 30;",
                "ALTER TABLE spawned_npcs ADD COLUMN respawn INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE spawned_npcs ADD COLUMN respawn_delay INTEGER NOT NULL DEFAULT 60;",
                "ALTER TABLE spawned_npcs ADD COLUMN pos_x REAL;",
                "ALTER TABLE spawned_npcs ADD COLUMN pos_y REAL;",
                "ALTER TABLE spawned_npcs ADD COLUMN pos_z REAL;",
                "ALTER TABLE spawned_npcs ADD COLUMN status TEXT NOT NULL DEFAULT 'alive';",
            };

            foreach (var sql in migrations)
            {
                try { _sqlite.ExecuteNonQuery(Sql.Builder.Append(sql), _db); }
                catch { /* column already exists */ }
            }
        }

        private void CloseDatabase()
        {
            if (_db != null)
                _sqlite.CloseDb(_db);
        }

        private void LoadNpcsFromDb()
        {
            string query = "SELECT npc_id, invulnerable FROM spawned_npcs WHERE status IN ('alive', 'pending');";
            _sqlite.Query(Sql.Builder.Append(query), _db, results =>
            {
                if (results == null) return;

                foreach (var row in results)
                {
                    ulong npcId = ulong.Parse(row["npc_id"].ToString());
                    bool invuln = Convert.ToInt32(row["invulnerable"]) == 1;

                    spawnedNpcs.Add(npcId);
                    if (invuln)
                        invulnerableNpcs.Add(npcId);
                }

                Puts($"Loaded {spawnedNpcs.Count} NPCs from database.");
            });
        }

        #endregion

        #region Command Queue

        private void PollCommands()
        {
            if (_db == null) return;

            string query = "SELECT id, action, payload FROM npc_commands WHERE status = 'pending' ORDER BY id ASC LIMIT 5;";
            _sqlite.Query(Sql.Builder.Append(query), _db, results =>
            {
                if (results == null || results.Count == 0) return;

                foreach (var row in results)
                {
                    int cmdId = Convert.ToInt32(row["id"]);
                    string action = row["action"].ToString();
                    string payload = row["payload"]?.ToString() ?? "{}";

                    // Mark as processing
                    _sqlite.ExecuteNonQuery(
                        Sql.Builder.Append("UPDATE npc_commands SET status = 'processing' WHERE id = @0;", cmdId), _db);

                    try
                    {
                        ProcessCommand(cmdId, action, payload);
                    }
                    catch (Exception ex)
                    {
                        CompleteCommand(cmdId, "failed", ex.Message);
                        PrintError($"Command {cmdId} ({action}) failed: {ex.Message}");
                    }
                }
            });
        }

        private void ProcessCommand(int cmdId, string action, string payload)
        {
            var data = JObject.Parse(payload);

            switch (action)
            {
                case "spawn":
                    HandleSpawn(cmdId, data);
                    break;
                case "remove":
                    HandleRemove(cmdId, data);
                    break;
                case "remove_all":
                    HandleRemoveAll(cmdId);
                    break;
                case "update":
                    HandleUpdate(cmdId, data);
                    break;
                default:
                    CompleteCommand(cmdId, "failed", $"Unknown action: {action}");
                    break;
            }
        }

        private void CompleteCommand(int cmdId, string status, string result)
        {
            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append(
                    "UPDATE npc_commands SET status = @0, result = @1, processed_at = datetime('now') WHERE id = @2;",
                    status, result, cmdId),
                _db);
        }

        #endregion

        #region Command Handlers

        private void HandleSpawn(int cmdId, JObject data)
        {
            if (HumanNPC == null)
            {
                CompleteCommand(cmdId, "failed", "HumanNPC plugin is not loaded");
                return;
            }

            string steamIdStr = data.Value<string>("steamId") ?? "";
            ulong steamId;
            if (!ulong.TryParse(steamIdStr, out steamId))
            {
                CompleteCommand(cmdId, "failed", "Invalid Steam ID");
                return;
            }

            var target = BasePlayer.FindByID(steamId);
            if (target == null)
            {
                CompleteCommand(cmdId, "failed", "Player not found or not online");
                return;
            }

            string npcName = data.Value<string>("name") ?? "NPC";
            float health = data.Value<float?>("health") ?? 100f;
            string kit = data.Value<string>("kit");
            bool hostile = data.Value<bool?>("hostile") ?? false;
            bool invulnerable = data.Value<bool?>("invulnerable") ?? false;
            bool lootable = data.Value<bool?>("lootable") ?? true;
            float damage = data.Value<float?>("damage") ?? 10f;
            float speed = data.Value<float?>("speed") ?? 3f;
            float detectRadius = data.Value<float?>("detectRadius") ?? 30f;
            bool respawn = data.Value<bool?>("respawn") ?? false;
            int respawnDelay = data.Value<int?>("respawnDelay") ?? 60;

            var position = target.transform.position + target.eyes.BodyForward() * 3f;
            position.y = TerrainMeta.HeightMap.GetHeight(position);
            var rotation = Quaternion.LookRotation(target.transform.position - position);

            var result = HumanNPC.Call("CreateNPCHook", position, rotation, npcName, (ulong)0, true);
            if (result == null)
            {
                CompleteCommand(cmdId, "failed", "CreateNPCHook returned null");
                return;
            }

            var npcPlayer = result as BasePlayer;
            if (npcPlayer == null)
            {
                CompleteCommand(cmdId, "failed", $"Unexpected return type: {result.GetType().Name}");
                return;
            }

            ulong npcId = npcPlayer.userID;
            spawnedNpcs.Add(npcId);

            if (invulnerable)
                invulnerableNpcs.Add(npcId);

            // Save to DB immediately
            string upsert = @"
                INSERT INTO spawned_npcs (npc_id, name, health, kit, hostile, invulnerable, lootable, damage, speed, detect_radius, respawn, respawn_delay, pos_x, pos_y, pos_z, status)
                VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, 'alive')
                ON CONFLICT(npc_id) DO UPDATE SET
                    name=@1, health=@2, kit=@3, hostile=@4, invulnerable=@5, lootable=@6,
                    damage=@7, speed=@8, detect_radius=@9, respawn=@10, respawn_delay=@11,
                    pos_x=@12, pos_y=@13, pos_z=@14, status='alive';";

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append(upsert,
                    npcId.ToString(), npcName, health, kit ?? "",
                    hostile ? 1 : 0, invulnerable ? 1 : 0, lootable ? 1 : 0,
                    damage, speed, detectRadius,
                    respawn ? 1 : 0, respawnDelay,
                    position.x, position.y, position.z),
                _db);

            Puts($"[Spawn] NPC created: {npcId} name={npcName}");

            // Wait for HumanPlayer component to initialize, then apply all settings
            ApplySettingsWhenReady(npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay);
        }

        private void ApplySettingsWhenReady(ulong npcId, int cmdId, float health, string kit,
            bool hostile, bool invulnerable, bool lootable, float damage, float speed,
            float detectRadius, bool respawn, int respawnDelay, int retries = 10)
        {
            timer.Once(0.5f, () =>
            {
                var npcPlayer = BasePlayer.FindByID(npcId);
                if (npcPlayer == null)
                {
                    if (retries > 0)
                    {
                        ApplySettingsWhenReady(npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay, retries - 1);
                        return;
                    }
                    CompleteCommand(cmdId, "failed", "NPC disappeared before settings could be applied");
                    return;
                }

                var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                if (humanPlayer == null)
                {
                    if (retries > 0)
                    {
                        Puts($"[Spawn] HumanPlayer not ready for {npcId}, retrying ({retries} left)");
                        ApplySettingsWhenReady(npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay, retries - 1);
                        return;
                    }
                    CompleteCommand(cmdId, "failed", "HumanPlayer component never initialized");
                    return;
                }

                // Apply all settings atomically
                try
                {
                    // Health
                    npcPlayer.health = health;
                    npcPlayer._maxHealth = health;
                    npcPlayer.SendNetworkUpdate();

                    // Get HumanNPC info object via reflection
                    var infoField = humanPlayer.GetType().GetField("info");
                    if (infoField != null)
                    {
                        var info = infoField.GetValue(humanPlayer);
                        if (info != null)
                        {
                            SetField(info, "damageAmount", damage);
                            SetField(info, "speed", speed);
                            SetField(info, "collisionRadius", detectRadius);
                            SetField(info, "hostile", hostile);
                            SetField(info, "lootable", lootable);
                            SetField(info, "invulnerability", invulnerable);
                            SetField(info, "respawn", respawn);
                            SetField(info, "respawnSeconds", (float)respawnDelay);

                            // Kit
                            if (!string.IsNullOrEmpty(kit))
                            {
                                var spawnkitField = info.GetType().GetField("spawnkit");
                                if (spawnkitField != null)
                                    spawnkitField.SetValue(info, kit);
                            }

                            HumanNPC.Call("UpdateNPC", npcPlayer, true);
                        }
                    }

                    // Invulnerability via our own hook
                    SetHumanNpcInvulnerability(npcPlayer, invulnerable);

                    // Apply kit items
                    if (!string.IsNullOrEmpty(kit) && Kits != null)
                    {
                        npcPlayer.inventory.Strip();
                        Kits.Call("GiveKit", npcPlayer, kit);
                    }

                    Puts($"[Spawn] Settings applied for NPC {npcId}");
                    CompleteCommand(cmdId, "done", npcId.ToString());
                }
                catch (Exception ex)
                {
                    PrintWarning($"[Spawn] Failed to apply settings for NPC {npcId}: {ex.Message}");
                    CompleteCommand(cmdId, "done", npcId.ToString()); // NPC exists, settings partially applied
                }
            });
        }

        private void HandleRemove(int cmdId, JObject data)
        {
            if (HumanNPC == null)
            {
                CompleteCommand(cmdId, "failed", "HumanNPC plugin is not loaded");
                return;
            }

            string npcIdStr = data.Value<string>("npcId") ?? "";
            ulong npcId;
            if (!ulong.TryParse(npcIdStr, out npcId))
            {
                CompleteCommand(cmdId, "failed", "Invalid NPC ID");
                return;
            }

            // Kill first if alive
            var npcPlayer = BasePlayer.FindByID(npcId);
            if (npcPlayer != null && !npcPlayer.IsDead())
            {
                // Temporarily remove invulnerability so Kill works
                invulnerableNpcs.Remove(npcId);
                npcPlayer.Die();
            }

            HumanNPC.Call("RemoveNPC", npcId);
            spawnedNpcs.Remove(npcId);
            invulnerableNpcs.Remove(npcId);

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append("UPDATE spawned_npcs SET status = 'removed' WHERE npc_id = @0;", npcId.ToString()), _db);

            Puts($"[Remove] NPC {npcId} removed");
            CompleteCommand(cmdId, "done", "removed");
        }

        private void HandleRemoveAll(int cmdId)
        {
            if (HumanNPC == null)
            {
                CompleteCommand(cmdId, "failed", "HumanNPC plugin is not loaded");
                return;
            }

            int count = 0;
            foreach (var npcId in spawnedNpcs.ToList())
            {
                var npcPlayer = BasePlayer.FindByID(npcId);
                if (npcPlayer != null && !npcPlayer.IsDead())
                {
                    invulnerableNpcs.Remove(npcId);
                    npcPlayer.Die();
                }

                HumanNPC.Call("RemoveNPC", npcId);
                count++;
            }

            spawnedNpcs.Clear();
            invulnerableNpcs.Clear();

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append("UPDATE spawned_npcs SET status = 'removed' WHERE status != 'removed';"), _db);

            Puts($"[RemoveAll] Removed {count} NPCs");
            CompleteCommand(cmdId, "done", count.ToString());
        }

        private void HandleUpdate(int cmdId, JObject data)
        {
            if (HumanNPC == null)
            {
                CompleteCommand(cmdId, "failed", "HumanNPC plugin is not loaded");
                return;
            }

            string npcIdStr = data.Value<string>("npcId") ?? "";
            ulong npcId;
            if (!ulong.TryParse(npcIdStr, out npcId))
            {
                CompleteCommand(cmdId, "failed", "Invalid NPC ID");
                return;
            }

            string field = data.Value<string>("field") ?? "";
            var valueToken = data["value"];
            if (string.IsNullOrEmpty(field) || valueToken == null)
            {
                CompleteCommand(cmdId, "failed", "Missing field or value");
                return;
            }

            var npcPlayer = BasePlayer.FindByID(npcId);
            if (npcPlayer == null)
            {
                CompleteCommand(cmdId, "failed", "NPC not found in game");
                return;
            }

            try
            {
                switch (field)
                {
                    case "health":
                        float hp = valueToken.Value<float>();
                        npcPlayer.health = hp;
                        npcPlayer._maxHealth = hp;
                        npcPlayer.SendNetworkUpdate();
                        UpdateDbField(npcId, "health", hp);
                        break;

                    case "kit":
                        string kitName = valueToken.Value<string>();
                        if (Kits != null && !string.IsNullOrEmpty(kitName))
                        {
                            npcPlayer.inventory.Strip();
                            Kits.Call("GiveKit", npcPlayer, kitName);
                            SetHumanNpcInfoField(npcPlayer, "spawnkit", kitName);
                        }
                        UpdateDbField(npcId, "kit", kitName);
                        break;

                    case "invulnerable":
                        bool invuln = valueToken.Value<bool>();
                        if (invuln) invulnerableNpcs.Add(npcId);
                        else invulnerableNpcs.Remove(npcId);
                        SetHumanNpcInvulnerability(npcPlayer, invuln);
                        UpdateDbField(npcId, "invulnerable", invuln ? 1 : 0);
                        break;

                    case "hostile":
                        bool hostileVal = valueToken.Value<bool>();
                        SetHumanNpcInfoField(npcPlayer, "hostile", hostileVal);
                        UpdateDbField(npcId, "hostile", hostileVal ? 1 : 0);
                        break;

                    case "lootable":
                        bool lootVal = valueToken.Value<bool>();
                        SetHumanNpcInfoField(npcPlayer, "lootable", lootVal);
                        UpdateDbField(npcId, "lootable", lootVal ? 1 : 0);
                        break;

                    case "damage":
                        float dmg = valueToken.Value<float>();
                        SetHumanNpcInfoField(npcPlayer, "damageAmount", dmg);
                        UpdateDbField(npcId, "damage", dmg);
                        break;

                    case "speed":
                        float spd = valueToken.Value<float>();
                        SetHumanNpcInfoField(npcPlayer, "speed", spd);
                        UpdateDbField(npcId, "speed", spd);
                        break;

                    case "detect_radius":
                        float rad = valueToken.Value<float>();
                        SetHumanNpcInfoField(npcPlayer, "collisionRadius", rad);
                        UpdateDbField(npcId, "detect_radius", rad);
                        break;

                    default:
                        CompleteCommand(cmdId, "failed", $"Unknown field: {field}");
                        return;
                }

                CompleteCommand(cmdId, "done", "updated");
            }
            catch (Exception ex)
            {
                CompleteCommand(cmdId, "failed", ex.Message);
            }
        }

        #endregion

        #region Position Tracking

        private void UpdatePositions()
        {
            if (_db == null) return;

            foreach (var npcId in spawnedNpcs.ToList())
            {
                var player = BasePlayer.FindByID(npcId);
                if (player != null && !player.IsDead())
                {
                    var pos = player.transform.position;
                    _sqlite.ExecuteNonQuery(
                        Sql.Builder.Append(
                            "UPDATE spawned_npcs SET pos_x = @0, pos_y = @1, pos_z = @2, status = 'alive' WHERE npc_id = @3;",
                            pos.x, pos.y, pos.z, npcId.ToString()),
                        _db);
                }
                else
                {
                    // Check if NPC is dead but should still be tracked (respawn)
                    _sqlite.ExecuteNonQuery(
                        Sql.Builder.Append(
                            "UPDATE spawned_npcs SET status = 'dead' WHERE npc_id = @0 AND status = 'alive';",
                            npcId.ToString()),
                        _db);
                }
            }

            // Clean old completed commands (older than 1 hour)
            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append("DELETE FROM npc_commands WHERE status IN ('done', 'failed') AND created_at < datetime('now', '-1 hour');"),
                _db);
        }

        #endregion

        #region Helpers

        private void SetField(object info, string fieldName, object value)
        {
            var field = info.GetType().GetField(fieldName);
            if (field == null) return;

            if (field.FieldType == typeof(float) && value is double)
                field.SetValue(info, (float)(double)value);
            else if (field.FieldType == typeof(float) && value is int)
                field.SetValue(info, (float)(int)value);
            else if (field.FieldType == typeof(float))
                field.SetValue(info, Convert.ToSingle(value));
            else if (field.FieldType == typeof(bool))
                field.SetValue(info, Convert.ToBoolean(value));
            else if (field.FieldType == typeof(string))
                field.SetValue(info, value.ToString());
            else
                field.SetValue(info, value);
        }

        private void SetHumanNpcInvulnerability(BasePlayer npcPlayer, bool invuln)
        {
            try
            {
                var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                if (humanPlayer == null) return;

                var infoField = humanPlayer.GetType().GetField("info");
                if (infoField == null) return;

                var info = infoField.GetValue(humanPlayer);
                if (info == null) return;

                SetField(info, "invulnerability", invuln);
                HumanNPC.Call("UpdateNPC", npcPlayer, true);
            }
            catch (Exception ex)
            {
                PrintWarning($"Failed to set invulnerability: {ex.Message}");
            }
        }

        private void SetHumanNpcInfoField(BasePlayer npcPlayer, string fieldName, object value)
        {
            try
            {
                var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                if (humanPlayer == null) return;

                var infoField = humanPlayer.GetType().GetField("info");
                if (infoField == null) return;

                var info = infoField.GetValue(humanPlayer);
                if (info == null) return;

                SetField(info, fieldName, value);
                HumanNPC.Call("UpdateNPC", npcPlayer, true);
            }
            catch (Exception ex)
            {
                PrintWarning($"Failed to set {fieldName}: {ex.Message}");
            }
        }

        private void UpdateDbField(ulong npcId, string field, object value)
        {
            string sql = $"UPDATE spawned_npcs SET {field} = @0 WHERE npc_id = @1;";
            _sqlite.ExecuteNonQuery(Sql.Builder.Append(sql, value, npcId.ToString()), _db);
        }

        #endregion
    }
}
