using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NpcAdmin", "rust-gg", "2.0.0")]
    [Description("RCON bridge for HumanNPC — spawn and manage NPCs via console commands with SQLite persistence")]
    public class NpcAdmin : RustPlugin
    {
        [PluginReference]
        private Plugin HumanNPC;

        [PluginReference]
        private Plugin Kits;

        private HashSet<ulong> invulnerableNpcs = new HashSet<ulong>();
        private HashSet<ulong> spawnedNpcs = new HashSet<ulong>();
        private Dictionary<ulong, NpcDbRecord> _npcRecords = new Dictionary<ulong, NpcDbRecord>();

        private readonly Core.SQLite.Libraries.SQLite _sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
        private Connection _db;

        #region Data Classes

        private class NpcDbRecord
        {
            public string Name;
            public string Kit;
            public bool Invulnerable;
            public bool Hostile;
            public float Health;
        }

        private class NpcInfo
        {
            public ulong Id;
            public string Name;
            public float X, Y, Z;
            public float Health;
            public string Kit;
            public bool Invulnerable;
            public bool Hostile;
            public bool Online;
        }

        #endregion

        #region Hooks

        private void Loaded()
        {
            OpenDatabase();
        }

        private void Unload()
        {
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

            string createTable = @"
                CREATE TABLE IF NOT EXISTS spawned_npcs (
                    npc_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL DEFAULT 'NPC',
                    kit TEXT,
                    invulnerable INTEGER NOT NULL DEFAULT 0,
                    hostile INTEGER NOT NULL DEFAULT 0,
                    health REAL NOT NULL DEFAULT 100,
                    created_at TEXT NOT NULL DEFAULT (datetime('now'))
                );";

            _sqlite.ExecuteNonQuery(Sql.Builder.Append(createTable), _db);
            LoadNpcsFromDb();
        }

        private void CloseDatabase()
        {
            if (_db != null)
                _sqlite.CloseDb(_db);
        }

        private void LoadNpcsFromDb()
        {
            string query = "SELECT npc_id, name, kit, invulnerable, hostile, health FROM spawned_npcs;";
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

                    _npcRecords[npcId] = new NpcDbRecord
                    {
                        Name = row["name"].ToString(),
                        Kit = row["kit"]?.ToString(),
                        Invulnerable = invuln,
                        Hostile = Convert.ToInt32(row["hostile"]) == 1,
                        Health = Convert.ToSingle(row["health"])
                    };
                }

                Puts($"Loaded {_npcRecords.Count} NPC records from database.");
            });
        }

        private void SaveNpcToDb(ulong npcId, string name, string kit, bool invulnerable, bool hostile, float health)
        {
            string upsert = @"
                INSERT INTO spawned_npcs (npc_id, name, kit, invulnerable, hostile, health, created_at)
                VALUES (@0, @1, @2, @3, @4, @5, datetime('now'))
                ON CONFLICT(npc_id) DO UPDATE SET
                    name = @1,
                    kit = @2,
                    invulnerable = @3,
                    hostile = @4,
                    health = @5;";

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append(upsert,
                    npcId.ToString(),
                    name,
                    kit ?? "",
                    invulnerable ? 1 : 0,
                    hostile ? 1 : 0,
                    health),
                _db);
        }

        private void UpdateNpcFieldInDb(ulong npcId, string field, object value)
        {
            string sql = $"UPDATE spawned_npcs SET {field} = @0 WHERE npc_id = @1;";
            _sqlite.ExecuteNonQuery(Sql.Builder.Append(sql, value, npcId.ToString()), _db);
        }

        private void DeleteNpcFromDb(ulong npcId)
        {
            _sqlite.ExecuteNonQuery(Sql.Builder.Append("DELETE FROM spawned_npcs WHERE npc_id = @0;", npcId.ToString()), _db);
        }

        private void DeleteAllNpcsFromDb()
        {
            _sqlite.ExecuteNonQuery(Sql.Builder.Append("DELETE FROM spawned_npcs;"), _db);
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("npcadmin.spawn")]
        private void CmdSpawn(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Usage: npcadmin.spawn <steamid> [\"NPC Name\"]");
                return;
            }

            if (HumanNPC == null)
            {
                arg.ReplyWith("ERROR: HumanNPC plugin is not loaded");
                return;
            }

            ulong steamId;
            if (!ulong.TryParse(arg.Args[0], out steamId))
            {
                arg.ReplyWith("ERROR: Invalid Steam ID");
                return;
            }

            var target = BasePlayer.FindByID(steamId);
            if (target == null)
            {
                arg.ReplyWith("ERROR: Player not found or not online");
                return;
            }

            string npcName = arg.Args.Length >= 2 ? arg.Args[1] : "NPC";

            var position = target.transform.position + target.eyes.BodyForward() * 3f;
            position.y = TerrainMeta.HeightMap.GetHeight(position);

            var rotation = Quaternion.LookRotation(target.transform.position - position);

            var result = HumanNPC.Call("CreateNPCHook", position, rotation, npcName, (ulong)0, true);
            if (result == null)
            {
                arg.ReplyWith("ERROR: CreateNPCHook returned null — check HumanNPC plugin logs");
                return;
            }

            var npcPlayer = result as BasePlayer;
            if (npcPlayer == null)
            {
                arg.ReplyWith($"ERROR: Unexpected return type: {result.GetType().Name}");
                return;
            }

            spawnedNpcs.Add(npcPlayer.userID);
            _npcRecords[npcPlayer.userID] = new NpcDbRecord
            {
                Name = npcName,
                Kit = null,
                Invulnerable = false,
                Hostile = false,
                Health = 100f
            };
            SaveNpcToDb(npcPlayer.userID, npcName, null, false, false, 100f);

            // Delay so HumanNPC finishes component setup in NextTick
            var npcId = npcPlayer.userID;
            timer.Once(0.5f, () =>
            {
                var npc = BasePlayer.FindByID(npcId);
                if (npc != null)
                    SetHumanNpcInvulnerability(npc, false);
            });

            arg.ReplyWith($"OK:{npcPlayer.userID}");
        }

        [ConsoleCommand("npcadmin.remove")]
        private void CmdRemove(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Usage: npcadmin.remove <npcid>");
                return;
            }

            if (HumanNPC == null)
            {
                arg.ReplyWith("ERROR: HumanNPC plugin is not loaded");
                return;
            }

            ulong npcId;
            if (!ulong.TryParse(arg.Args[0], out npcId))
            {
                arg.ReplyWith("ERROR: Invalid NPC ID");
                return;
            }

            HumanNPC.Call("RemoveNPC", npcId);
            spawnedNpcs.Remove(npcId);
            invulnerableNpcs.Remove(npcId);
            _npcRecords.Remove(npcId);
            DeleteNpcFromDb(npcId);
            arg.ReplyWith("OK:removed");
        }

        [ConsoleCommand("npcadmin.removeall")]
        private void CmdRemoveAll(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (HumanNPC == null)
            {
                arg.ReplyWith("ERROR: HumanNPC plugin is not loaded");
                return;
            }

            int count = 0;
            var npcs = GetNpcList();
            foreach (var npc in npcs)
            {
                HumanNPC.Call("RemoveNPC", npc.Id);
                invulnerableNpcs.Remove(npc.Id);
                count++;
            }
            spawnedNpcs.Clear();
            _npcRecords.Clear();
            DeleteAllNpcsFromDb();

            arg.ReplyWith($"OK:{count}");
        }

        [ConsoleCommand("npcadmin.list")]
        private void CmdList(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (HumanNPC == null)
            {
                arg.ReplyWith("[]");
                return;
            }

            var npcs = GetNpcList();
            arg.ReplyWith(JsonConvert.SerializeObject(npcs));
        }

        [ConsoleCommand("npcadmin.set")]
        private void CmdSet(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (arg.Args == null || arg.Args.Length < 3)
            {
                arg.ReplyWith("Usage: npcadmin.set <npcid> <option> <value>");
                return;
            }

            if (HumanNPC == null)
            {
                arg.ReplyWith("ERROR: HumanNPC plugin is not loaded");
                return;
            }

            ulong npcId;
            if (!ulong.TryParse(arg.Args[0], out npcId))
            {
                arg.ReplyWith("ERROR: Invalid NPC ID");
                return;
            }

            string option = arg.Args[1].ToLower();
            string value = string.Join(" ", arg.Args.Skip(2));

            var npcPlayer = BasePlayer.FindByID(npcId);
            if (npcPlayer == null)
            {
                arg.ReplyWith("ERROR: NPC not found");
                return;
            }

            // Check if HumanPlayer component is ready; if not, retry after a delay
            var humanPlayerCheck = npcPlayer.GetComponent("HumanPlayer");
            if (humanPlayerCheck == null)
            {
                ApplySettingDelayed(npcId, option, value);
                arg.ReplyWith("OK:updated");
                return;
            }

            ApplySetting(npcPlayer, npcId, option, value, arg);
        }

        private void ApplySettingDelayed(ulong npcId, string option, string value, int retries = 3)
        {
            timer.Once(0.5f, () =>
            {
                var npcPlayer = BasePlayer.FindByID(npcId);
                if (npcPlayer == null) return;

                var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                if (humanPlayer == null && retries > 0)
                {
                    ApplySettingDelayed(npcId, option, value, retries - 1);
                    return;
                }

                ApplySetting(npcPlayer, npcId, option, value, null);
            });
        }

        private void ApplySetting(BasePlayer npcPlayer, ulong npcId, string option, string value, ConsoleSystem.Arg arg)
        {
            switch (option)
            {
                case "health":
                    float hp;
                    if (float.TryParse(value, out hp))
                    {
                        npcPlayer.health = hp;
                        npcPlayer._maxHealth = hp;
                        npcPlayer.SendNetworkUpdate();
                        UpdateNpcFieldInDb(npcId, "health", hp);
                        if (_npcRecords.ContainsKey(npcId))
                            _npcRecords[npcId].Health = hp;
                    }
                    break;

                case "kit":
                    if (Kits == null)
                    {
                        arg?.ReplyWith("ERROR: Kits plugin is not loaded");
                        return;
                    }
                    npcPlayer.inventory.Strip();
                    var kitResult = Kits.Call("GiveKit", npcPlayer, value);
                    if (kitResult is string errMsg)
                    {
                        arg?.ReplyWith($"ERROR: Kit failed — {errMsg}");
                        return;
                    }
                    // Set spawnkit on HumanNPC info so kit persists across respawns
                    try
                    {
                        var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                        if (humanPlayer != null)
                        {
                            var infoField = humanPlayer.GetType().GetField("info");
                            if (infoField != null)
                            {
                                var info = infoField.GetValue(humanPlayer);
                                if (info != null)
                                {
                                    var spawnkitField = info.GetType().GetField("spawnkit");
                                    if (spawnkitField != null)
                                        spawnkitField.SetValue(info, value);
                                    HumanNPC.Call("UpdateNPC", npcPlayer, false);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"Failed to set spawnkit on HumanNPC: {ex.Message}");
                    }
                    npcPlayer.SendNetworkUpdate();
                    UpdateNpcFieldInDb(npcId, "kit", value);
                    if (_npcRecords.ContainsKey(npcId))
                        _npcRecords[npcId].Kit = value;
                    break;

                case "invulnerable":
                case "invulnerability":
                    bool invuln;
                    if (bool.TryParse(value, out invuln))
                    {
                        if (invuln)
                            invulnerableNpcs.Add(npcId);
                        else
                            invulnerableNpcs.Remove(npcId);

                        SetHumanNpcInvulnerability(npcPlayer, invuln);
                        UpdateNpcFieldInDb(npcId, "invulnerable", invuln ? 1 : 0);
                        if (_npcRecords.ContainsKey(npcId))
                            _npcRecords[npcId].Invulnerable = invuln;
                    }
                    break;

                case "hostile":
                case "lootable":
                case "respawn":
                case "damageamount":
                case "speed":
                case "radius":
                case "collisionradius":
                case "defend":
                case "evade":
                case "follow":
                    try
                    {
                        var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                        if (humanPlayer == null)
                        {
                            PrintWarning($"HumanPlayer component not found for NPC {npcId} — {option} not applied");
                            return;
                        }
                        var infoField = humanPlayer.GetType().GetField("info");
                        if (infoField == null) return;
                        var info = infoField.GetValue(humanPlayer);
                        if (info == null) return;
                        SetInfoProperty(info, option, value);
                        HumanNPC.Call("UpdateNPC", npcPlayer, false);

                        if (option == "hostile" && _npcRecords.ContainsKey(npcId))
                        {
                            bool hostileVal;
                            if (bool.TryParse(value, out hostileVal))
                            {
                                _npcRecords[npcId].Hostile = hostileVal;
                                UpdateNpcFieldInDb(npcId, "hostile", hostileVal ? 1 : 0);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"Failed to set {option} on NPC {npcId}: {ex.Message}");
                    }
                    break;

                default:
                    arg?.ReplyWith($"ERROR: Unknown option '{option}'");
                    return;
            }
        }

        private void SetInfoProperty(object info, string option, string value)
        {
            var type = info.GetType();

            string fieldName;
            switch (option)
            {
                case "radius":
                case "collisionradius":
                    fieldName = "collisionRadius";
                    break;
                case "damageamount":
                    fieldName = "damageAmount";
                    break;
                case "invulnerable":
                    fieldName = "invulnerability";
                    break;
                case "respawn":
                    var parts = value.Split(' ');
                    var respawnField = type.GetField("respawn");
                    if (respawnField != null)
                        respawnField.SetValue(info, bool.Parse(parts[0]));
                    if (parts.Length > 1)
                    {
                        var secondsField = type.GetField("respawnSeconds");
                        if (secondsField != null)
                            secondsField.SetValue(info, float.Parse(parts[1]));
                    }
                    return;
                default:
                    fieldName = option;
                    break;
            }

            var field = type.GetField(fieldName);
            if (field == null) return;

            if (field.FieldType == typeof(float))
                field.SetValue(info, float.Parse(value));
            else if (field.FieldType == typeof(bool))
                field.SetValue(info, bool.Parse(value));
            else if (field.FieldType == typeof(string))
                field.SetValue(info, value);
        }

        [ConsoleCommand("npcadmin.give")]
        private void CmdGive(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin) return;

            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Usage: npcadmin.give <npcid> <itemname> [belt|wear|main]");
                return;
            }

            if (HumanNPC == null)
            {
                arg.ReplyWith("ERROR: HumanNPC plugin is not loaded");
                return;
            }

            ulong npcId;
            if (!ulong.TryParse(arg.Args[0], out npcId))
            {
                arg.ReplyWith("ERROR: Invalid NPC ID");
                return;
            }

            string itemShortname = arg.Args[1];
            string container = arg.Args.Length >= 3 ? arg.Args[2] : "belt";

            var npcPlayer = BasePlayer.FindByID(npcId);
            if (npcPlayer == null)
            {
                arg.ReplyWith("ERROR: NPC not found");
                return;
            }

            var itemDef = ItemManager.FindItemDefinition(itemShortname);
            if (itemDef == null)
            {
                arg.ReplyWith($"ERROR: Unknown item '{itemShortname}'");
                return;
            }

            var item = ItemManager.Create(itemDef);
            if (item == null)
            {
                arg.ReplyWith("ERROR: Failed to create item");
                return;
            }

            ItemContainer targetContainer;
            switch (container.ToLower())
            {
                case "wear":
                    targetContainer = npcPlayer.inventory.containerWear;
                    break;
                case "main":
                    targetContainer = npcPlayer.inventory.containerMain;
                    break;
                default:
                    targetContainer = npcPlayer.inventory.containerBelt;
                    break;
            }

            if (!item.MoveToContainer(targetContainer))
            {
                item.Remove();
                arg.ReplyWith("ERROR: Failed to move item to container (full?)");
                return;
            }

            arg.ReplyWith("OK:given");
        }

        #endregion

        #region Helpers

        private void SetHumanNpcInvulnerability(BasePlayer npcPlayer, bool invuln)
        {
            try
            {
                var humanPlayer = npcPlayer.GetComponent("HumanPlayer");
                if (humanPlayer != null)
                {
                    var infoField = humanPlayer.GetType().GetField("info");
                    if (infoField != null)
                    {
                        var info = infoField.GetValue(humanPlayer);
                        if (info != null)
                        {
                            SetInfoProperty(info, "invulnerable", invuln.ToString());
                            HumanNPC.Call("UpdateNPC", npcPlayer, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PrintWarning($"Failed to set invulnerability on HumanNPC: {ex.Message}");
            }
        }

        private List<NpcInfo> GetNpcList()
        {
            var result = new List<NpcInfo>();

            foreach (var npcId in spawnedNpcs)
            {
                var player = BasePlayer.FindByID(npcId);
                NpcDbRecord record = null;
                _npcRecords.TryGetValue(npcId, out record);

                if (player != null && !player.IsDead())
                {
                    result.Add(new NpcInfo
                    {
                        Id = player.userID,
                        Name = player.displayName ?? "NPC",
                        X = player.transform.position.x,
                        Y = player.transform.position.y,
                        Z = player.transform.position.z,
                        Health = player.health,
                        Kit = record?.Kit,
                        Invulnerable = invulnerableNpcs.Contains(player.userID),
                        Hostile = record != null ? record.Hostile : false,
                        Online = true
                    });
                }
                else if (record != null)
                {
                    result.Add(new NpcInfo
                    {
                        Id = npcId,
                        Name = record.Name ?? "NPC",
                        Health = record.Health,
                        Kit = record.Kit,
                        Invulnerable = record.Invulnerable,
                        Hostile = record.Hostile,
                        Online = false
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
