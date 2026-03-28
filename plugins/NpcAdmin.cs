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
    [Info("NpcAdmin", "rust-gg", "4.0.0")]
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

            MigrateSchema();
            LoadNpcsFromDb();
        }

        private void MigrateSchema()
        {
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

        #region NPC Lookup

        /// <summary>
        /// Get the HumanPlayer component directly from a BasePlayer entity.
        /// Can't use GetComponent("HumanPlayer") because it's a nested class in another plugin.
        /// Instead iterate all components and match by type name.
        /// </summary>
        private object GetHumanPlayerComponent(BasePlayer player)
        {
            if (player == null) return null;
            foreach (var comp in player.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == "HumanPlayer")
                    return comp;
            }
            return null;
        }

        /// <summary>
        /// Find a HumanPlayer component by NPC ID using HumanNPC's string-based lookup.
        /// FindHumanPlayerByID(ulong) fails via Oxide.Call due to parameter boxing issues.
        /// FindHumanPlayer(string) searches by UserIDString and works reliably.
        /// </summary>
        private object FindHumanPlayer(ulong npcId)
        {
            if (HumanNPC == null) return null;
            return HumanNPC.Call("FindHumanPlayer", npcId.ToString());
        }

        /// <summary>
        /// Get the BasePlayer entity from a HumanPlayer component.
        /// </summary>
        private BasePlayer GetPlayerFromHumanPlayer(object humanPlayer)
        {
            if (humanPlayer == null) return null;
            var playerField = humanPlayer.GetType().GetField("player");
            if (playerField == null) return null;
            return playerField.GetValue(humanPlayer) as BasePlayer;
        }

        /// <summary>
        /// Find an NPC's BasePlayer by ID.
        /// </summary>
        private BasePlayer FindNpcPlayer(ulong npcId)
        {
            return GetPlayerFromHumanPlayer(FindHumanPlayer(npcId));
        }

        /// <summary>
        /// Get the HumanNPCInfo object from a HumanPlayer component.
        /// </summary>
        private object GetInfoFromHumanPlayer(object humanPlayer)
        {
            if (humanPlayer == null) return null;
            var infoField = humanPlayer.GetType().GetField("info");
            if (infoField == null) return null;
            return infoField.GetValue(humanPlayer);
        }

        /// <summary>
        /// Get the HumanNPCInfo object for an NPC by ID.
        /// </summary>
        private object GetNpcInfo(ulong npcId)
        {
            return GetInfoFromHumanPlayer(FindHumanPlayer(npcId));
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

                    _sqlite.ExecuteNonQuery(
                        Sql.Builder.Append("UPDATE npc_commands SET status = 'processing' WHERE id = @0;", cmdId), _db);

                    try
                    {
                        ProcessCommand(cmdId, action, payload);
                    }
                    catch (Exception ex)
                    {
                        CompleteCommand(cmdId, "failed", ex.Message);
                        PrintError($"Command {cmdId} ({action}) failed: {ex.Message}\n{ex.StackTrace}");
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

            // saved=false: don't persist in HumanNPC data file, our SQLite is the source of truth
            var result = HumanNPC.Call("CreateNPCHook", position, rotation, npcName, (ulong)0, false);
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

            // Save to our DB immediately
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

            // Apply settings using the npcPlayer ref we already have (don't re-lookup by ID)
            ApplySettingsWhenReady(npcPlayer, npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay);
        }

        private void ApplySettingsWhenReady(BasePlayer npcPlayer, ulong npcId, int cmdId, float health, string kit,
            bool hostile, bool invulnerable, bool lootable, float damage, float speed,
            float detectRadius, bool respawn, int respawnDelay, int retries = 10)
        {
            timer.Once(0.5f, () =>
            {
                try
                {
                    if (npcPlayer == null || npcPlayer.IsDestroyed)
                        npcPlayer = FindNpcPlayer(npcId);

                    object humanPlayer = npcPlayer != null ? GetHumanPlayerComponent(npcPlayer) : null;

                    if (humanPlayer == null)
                    {
                        if (retries > 0)
                        {
                            Puts($"[Spawn] HumanPlayer not found for {npcId}, retrying ({retries} left)");
                            ApplySettingsWhenReady(npcPlayer, npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay, retries - 1);
                            return;
                        }
                        CompleteCommand(cmdId, "failed", "HumanPlayer component never initialized");
                        return;
                    }

                    var info = GetInfoFromHumanPlayer(humanPlayer);
                    if (info == null)
                    {
                        if (retries > 0)
                        {
                            Puts($"[Spawn] NPC info not ready for {npcId}, retrying ({retries} left)");
                            ApplySettingsWhenReady(npcPlayer, npcId, cmdId, health, kit, hostile, invulnerable, lootable, damage, speed, detectRadius, respawn, respawnDelay, retries - 1);
                            return;
                        }
                        CompleteCommand(cmdId, "failed", "NPC info never initialized");
                        return;
                    }
                    if (npcPlayer == null)
                    {
                        CompleteCommand(cmdId, "failed", "Could not get BasePlayer from HumanPlayer");
                        return;
                    }

                    // Override HumanNPC defaults (invulnerability=true, respawn=true, health=50)
                    // Must set these BEFORE RefreshNPC so SpawnNPC reads correct values
                    SetField(info, "health", health);
                    SetField(info, "invulnerability", invulnerable);
                    SetField(info, "hostile", hostile);
                    SetField(info, "lootable", lootable);
                    SetField(info, "damageAmount", damage);
                    SetField(info, "speed", speed);
                    SetField(info, "collisionRadius", detectRadius);
                    SetField(info, "respawn", respawn);
                    SetField(info, "respawnSeconds", (float)respawnDelay);

                    if (!string.IsNullOrEmpty(kit))
                    {
                        var spawnkitField = info.GetType().GetField("spawnkit");
                        if (spawnkitField != null)
                            spawnkitField.SetValue(info, kit);
                    }

                    // RefreshNPC kills the entity and respawns via SpawnNPC, which calls
                    // UpdateHealth (applies health from info) and UpdateInventory (applies kit from info.spawnkit).
                    // This is essential — UpdateNPC does NOT call UpdateInventory or UpdateHealth.
                    HumanNPC.Call("RefreshNPC", npcPlayer, true);

                    // After respawn, re-apply health on the new BasePlayer entity
                    // (SpawnNPC creates a new entity so we must find it again)
                    timer.Once(1f, () =>
                    {
                        var p = FindNpcPlayer(npcId);
                        if (p != null)
                        {
                            p.health = health;
                            p._maxHealth = health;
                            p.SendNetworkUpdate();
                        }
                    });

                    Puts($"[Spawn] Settings applied for NPC {npcId}: invuln={invulnerable}, hostile={hostile}, hp={health}, kit={kit}, respawn={respawn}");
                    CompleteCommand(cmdId, "done", npcId.ToString());
                }
                catch (Exception ex)
                {
                    PrintError($"[Spawn] Exception applying settings for NPC {npcId}: {ex.Message}\n{ex.StackTrace}");
                    CompleteCommand(cmdId, "done", npcId.ToString());
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

            invulnerableNpcs.Remove(npcId);

            // RemoveNPC(ulong) removes from HumanNPC data and calls KillMessage on the entity
            HumanNPC.Call("RemoveNPC", npcId);
            spawnedNpcs.Remove(npcId);

            Puts($"[Remove] NPC {npcId} removed via RemoveNPC");

            // Verify entity is gone, force-kill if not
            timer.Once(0.5f, () =>
            {
                var npcPlayer = FindNpcPlayer(npcId);
                if (npcPlayer != null && !npcPlayer.IsDestroyed)
                {
                    Puts($"[Remove] NPC {npcId} still alive after RemoveNPC, force-killing");
                    npcPlayer.KillMessage();
                }
            });

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append("UPDATE spawned_npcs SET status = 'removed' WHERE npc_id = @0;", npcId.ToString()), _db);

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
                invulnerableNpcs.Remove(npcId);
                HumanNPC.Call("RemoveNPC", npcId);
                count++;
            }

            spawnedNpcs.Clear();
            invulnerableNpcs.Clear();

            // Force-kill any remaining entities
            timer.Once(0.5f, () =>
            {
                var allHumanPlayers = UnityEngine.Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                // Cleanup is handled by RemoveNPC + KillMessage
            });

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

            var npcPlayer = FindNpcPlayer(npcId);
            if (npcPlayer == null)
            {
                CompleteCommand(cmdId, "failed", "NPC not found in game");
                return;
            }

            try
            {
                var info = GetNpcInfo(npcId);
                switch (field)
                {
                    case "health":
                        float hp = valueToken.Value<float>();
                        npcPlayer.health = hp;
                        npcPlayer._maxHealth = hp;
                        npcPlayer.SendNetworkUpdate();
                        if (info != null) SetField(info, "health", hp);
                        UpdateDbField(npcId, "health", hp);
                        break;

                    case "kit":
                        string kitName = valueToken.Value<string>();
                        if (Kits != null && !string.IsNullOrEmpty(kitName))
                        {
                            npcPlayer.inventory.Strip();
                            Kits.Call("GiveKit", npcPlayer, kitName);
                            if (info != null) SetField(info, "spawnkit", kitName);
                        }
                        UpdateDbField(npcId, "kit", kitName);
                        break;

                    case "invulnerable":
                        bool invuln = valueToken.Value<bool>();
                        if (invuln) invulnerableNpcs.Add(npcId);
                        else invulnerableNpcs.Remove(npcId);
                        if (info != null) SetField(info, "invulnerability", invuln);
                        UpdateDbField(npcId, "invulnerable", invuln ? 1 : 0);
                        break;

                    case "hostile":
                        bool hostileVal = valueToken.Value<bool>();
                        if (info != null) SetField(info, "hostile", hostileVal);
                        UpdateDbField(npcId, "hostile", hostileVal ? 1 : 0);
                        break;

                    case "lootable":
                        bool lootVal = valueToken.Value<bool>();
                        if (info != null) SetField(info, "lootable", lootVal);
                        UpdateDbField(npcId, "lootable", lootVal ? 1 : 0);
                        break;

                    case "damage":
                        float dmg = valueToken.Value<float>();
                        if (info != null) SetField(info, "damageAmount", dmg);
                        UpdateDbField(npcId, "damage", dmg);
                        break;

                    case "speed":
                        float spd = valueToken.Value<float>();
                        if (info != null) SetField(info, "speed", spd);
                        UpdateDbField(npcId, "speed", spd);
                        break;

                    case "detect_radius":
                        float rad = valueToken.Value<float>();
                        if (info != null) SetField(info, "collisionRadius", rad);
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
                var player = FindNpcPlayer(npcId);
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
                    _sqlite.ExecuteNonQuery(
                        Sql.Builder.Append(
                            "UPDATE spawned_npcs SET status = 'dead' WHERE npc_id = @0 AND status = 'alive';",
                            npcId.ToString()),
                        _db);
                }
            }

            // Clean old completed commands
            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append("DELETE FROM npc_commands WHERE status IN ('done', 'failed') AND created_at < datetime('now', '-1 hour');"),
                _db);
        }

        #endregion

        #region Helpers

        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName);
            if (field == null)
            {
                Puts($"[SetField] Field '{fieldName}' not found on {obj.GetType().Name}");
                return;
            }

            if (field.FieldType == typeof(float) && value is double)
                field.SetValue(obj, (float)(double)value);
            else if (field.FieldType == typeof(float) && value is int)
                field.SetValue(obj, (float)(int)value);
            else if (field.FieldType == typeof(float))
                field.SetValue(obj, Convert.ToSingle(value));
            else if (field.FieldType == typeof(bool))
                field.SetValue(obj, Convert.ToBoolean(value));
            else if (field.FieldType == typeof(string))
                field.SetValue(obj, value.ToString());
            else
                field.SetValue(obj, value);
        }

        private void UpdateDbField(ulong npcId, string field, object value)
        {
            string sql = $"UPDATE spawned_npcs SET {field} = @0 WHERE npc_id = @1;";
            _sqlite.ExecuteNonQuery(Sql.Builder.Append(sql, value, npcId.ToString()), _db);
        }

        #endregion
    }
}
