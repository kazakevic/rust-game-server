using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GunGame", "rust-gg", "1.0.0")]
    [Description("Gun Game mod — XP-based progression with auto kit equip per level")]
    public class GunGame : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Kits;

        private readonly Core.SQLite.Libraries.SQLite _sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
        private Connection _db;

        private Dictionary<ulong, PlayerData> _playerCache = new Dictionary<ulong, PlayerData>();

        private const string PermAdmin = "gungame.admin";

        private bool _testMode = false;

        private readonly List<BaseEntity> _spawnedNPCs = new List<BaseEntity>();
        private readonly System.Random _rng = new System.Random();

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("XPPerKill")]
            public int XPPerKill { get; set; } = 100;

            [JsonProperty("HeadshotBonusXP")]
            public int HeadshotBonusXP { get; set; } = 50;

            [JsonProperty("DistanceBonusXPPer50m")]
            public int DistanceBonusXPPer50m { get; set; } = 25;

            [JsonProperty("KitPrefix")]
            public string KitPrefix { get; set; } = "level_";

            [JsonProperty("MaxLevel")]
            public int MaxLevel { get; set; } = 10;

            [JsonProperty("LevelXPThresholds")]
            public Dictionary<int, int> LevelXPThresholds { get; set; } = new Dictionary<int, int>
            {
                [2] = 200,
                [3] = 500,
                [4] = 900,
                [5] = 1400,
                [6] = 2000,
                [7] = 2700,
                [8] = 3500,
                [9] = 4500,
                [10] = 6000
            };

            [JsonProperty("WipeOnNewSave")]
            public bool WipeOnNewSave { get; set; } = true;

            [JsonProperty("ChatPrefix")]
            public string ChatPrefix { get; set; } = "<color=#ff6600>[GunGame]</color>";

            [JsonProperty("TopListSize")]
            public int TopListSize { get; set; } = 10;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                    throw new Exception();
            }
            catch
            {
                PrintWarning("Invalid config, loading defaults...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data Model

        private class PlayerData
        {
            public ulong SteamId { get; set; }
            public string DisplayName { get; set; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int Headshots { get; set; }
            public int XP { get; set; }
            public int Level { get; set; } = 1;
            public bool Dirty { get; set; }

            public double KDRatio => Deaths == 0 ? Kills : Math.Round((double)Kills / Deaths, 2);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XPGained"] = "You gained <color=#00ff00>+{0} XP</color> ({1} total) — Level {2}",
                ["LevelUp"] = "<color=#ffff00>LEVEL UP!</color> You are now <color=#00ffff>Level {0}</color>!",
                ["MaxLevel"] = "<color=#ffff00>You have reached the maximum level!</color>",
                ["Stats"] = "<color=#00ffff>--- Your Stats ---</color>\nLevel: {0} | XP: {1}/{2}\nKills: {3} | Deaths: {4} | K/D: {5}\nHeadshots: {6}",
                ["StatsOther"] = "<color=#00ffff>--- {0}'s Stats ---</color>\nLevel: {1} | XP: {2}\nKills: {3} | Deaths: {4} | K/D: {5}\nHeadshots: {6}",
                ["TopHeader"] = "<color=#00ffff>--- Gun Game Leaderboard ---</color>",
                ["TopEntry"] = "#{0} <color=#ffff00>{1}</color> — Level {2} | XP: {3} | Kills: {4} | K/D: {5}",
                ["TopEmpty"] = "No player data yet.",
                ["WipeComplete"] = "<color=#ff0000>All Gun Game data has been wiped!</color>",
                ["WipeConfirm"] = "Type <color=#ff0000>/gg wipe confirm</color> to confirm data wipe.",
                ["SetLevel"] = "Set <color=#00ffff>{0}</color> to level <color=#ffff00>{1}</color>.",
                ["PlayerNotFound"] = "Player not found.",
                ["InvalidLevel"] = "Invalid level. Must be between 1 and {0}.",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["Usage"] = "<color=#00ffff>Usage:</color>\n/gg — View your stats\n/gg top — Leaderboard\n/gg stats <player> — View player stats (admin)\n/gg setlevel <player> <level> — Set level (admin)\n/gg testmode — Toggle test mode: NPC kills grant XP (admin)\n/gg spawn [count] — Spawn test NPCs with random kits (admin)\n/gg despawn — Remove all test NPCs (admin)\n/gg wipe — Reset all data (admin)",
                ["KitNotFound"] = "<color=#ff0000>Kit '{0}' not found. Ask an admin to create it.</color>",
                ["KitsNotLoaded"] = "<color=#ff0000>Kits plugin is not loaded!</color>",
                ["TestModeOn"] = "<color=#00ff00>Test Mode ENABLED.</color> NPC kills now grant XP.",
                ["TestModeOff"] = "<color=#ff6600>Test Mode DISABLED.</color> NPC kills are ignored.",
                ["TestModeStatus"] = "Test Mode is currently <color=#ffff00>{0}</color>.",
                ["SpawnedNPCs"] = "<color=#00ff00>Spawned {0} test NPC(s)</color> with random level kits. Test mode enabled.",
                ["DespawnedNPCs"] = "<color=#ff6600>Removed {0} test NPC(s).</color>",
                ["NoNPCs"] = "No test NPCs to remove.",
                ["SpawnFailed"] = "<color=#ff0000>Failed to spawn NPC. Are you in a valid position?</color>"
            }, this);
        }

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            string msg = lang.GetMessage(key, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            player.ChatMessage($"{_config.ChatPrefix} {Lang(key, player, args)}");
        }

        #endregion

        #region Lifecycle

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
        }

        private void OnServerInitialized()
        {
            if (Kits == null)
                PrintWarning("Kits plugin not found! Gun Game requires the Kits plugin to equip weapons.");

            OpenDatabase();
        }

        private void Unload()
        {
            CleanupTestNPCs();
            SaveAllPlayers();
            CloseDatabase();
        }

        #endregion

        #region Database

        private void OpenDatabase()
        {
            _db = _sqlite.OpenDb("GunGame.db", this);
            if (_db == null)
            {
                PrintError("Failed to open SQLite database!");
                return;
            }

            string createTable = @"
                CREATE TABLE IF NOT EXISTS player_stats (
                    steam_id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    kills INTEGER NOT NULL DEFAULT 0,
                    deaths INTEGER NOT NULL DEFAULT 0,
                    headshots INTEGER NOT NULL DEFAULT 0,
                    xp INTEGER NOT NULL DEFAULT 0,
                    level INTEGER NOT NULL DEFAULT 1,
                    last_updated TEXT NOT NULL DEFAULT (datetime('now'))
                );";

            _sqlite.ExecuteNonQuery(Sql.Builder.Append(createTable), _db);

            LoadAllPlayers();
        }

        private void CloseDatabase()
        {
            if (_db != null)
                _sqlite.CloseDb(_db);
        }

        private void LoadAllPlayers()
        {
            string query = "SELECT steam_id, display_name, kills, deaths, headshots, xp, level FROM player_stats;";
            _sqlite.Query(Sql.Builder.Append(query), _db, results =>
            {
                if (results == null) return;

                foreach (var row in results)
                {
                    ulong steamId = ulong.Parse(row["steam_id"].ToString());
                    _playerCache[steamId] = new PlayerData
                    {
                        SteamId = steamId,
                        DisplayName = row["display_name"].ToString(),
                        Kills = Convert.ToInt32(row["kills"]),
                        Deaths = Convert.ToInt32(row["deaths"]),
                        Headshots = Convert.ToInt32(row["headshots"]),
                        XP = Convert.ToInt32(row["xp"]),
                        Level = Convert.ToInt32(row["level"]),
                        Dirty = false
                    };
                }

                Puts($"Loaded {_playerCache.Count} player records from database.");
            });
        }

        private void SavePlayer(PlayerData data)
        {
            if (!data.Dirty) return;

            string upsert = @"
                INSERT INTO player_stats (steam_id, display_name, kills, deaths, headshots, xp, level, last_updated)
                VALUES (@0, @1, @2, @3, @4, @5, @6, datetime('now'))
                ON CONFLICT(steam_id) DO UPDATE SET
                    display_name = @1,
                    kills = @2,
                    deaths = @3,
                    headshots = @4,
                    xp = @5,
                    level = @6,
                    last_updated = datetime('now');";

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append(upsert,
                    data.SteamId.ToString(),
                    data.DisplayName,
                    data.Kills,
                    data.Deaths,
                    data.Headshots,
                    data.XP,
                    data.Level),
                _db);

            data.Dirty = false;
        }

        private void SaveAllPlayers()
        {
            int saved = 0;
            foreach (var data in _playerCache.Values)
            {
                if (data.Dirty)
                {
                    SavePlayer(data);
                    saved++;
                }
            }
            if (saved > 0)
                Puts($"Saved {saved} player records to database.");
        }

        private void WipeDatabase()
        {
            _sqlite.ExecuteNonQuery(Sql.Builder.Append("DELETE FROM player_stats;"), _db);
            _playerCache.Clear();
            Puts("Gun Game data wiped.");
        }

        #endregion

        #region Player Data Helpers

        private PlayerData GetOrCreatePlayer(BasePlayer player)
        {
            if (_playerCache.TryGetValue(player.userID, out PlayerData data))
            {
                if (data.DisplayName != player.displayName)
                {
                    data.DisplayName = player.displayName;
                    data.Dirty = true;
                }
                return data;
            }

            data = new PlayerData
            {
                SteamId = player.userID,
                DisplayName = player.displayName,
                Level = 1,
                Dirty = true
            };
            _playerCache[player.userID] = data;
            return data;
        }

        private int CalculateXP(PlayerData killerData, HitInfo info)
        {
            int xpGained = _config.XPPerKill;

            if (info.isHeadshot)
            {
                xpGained += _config.HeadshotBonusXP;
                killerData.Headshots++;
            }

            if (info.IsProjectile())
            {
                int distanceBonusUnits = (int)(info.ProjectileDistance / 50f);
                if (distanceBonusUnits > 0)
                    xpGained += distanceBonusUnits * _config.DistanceBonusXPPer50m;
            }

            return xpGained;
        }

        private bool CheckLevelUp(PlayerData data)
        {
            bool leveledUp = false;
            while (data.Level < _config.MaxLevel && data.XP >= GetXPForNextLevel(data.Level))
            {
                data.Level++;
                leveledUp = true;
            }
            return leveledUp;
        }

        private int GetXPForNextLevel(int currentLevel)
        {
            int nextLevel = currentLevel + 1;
            if (nextLevel > _config.MaxLevel) return int.MaxValue;
            return _config.LevelXPThresholds.TryGetValue(nextLevel, out int threshold) ? threshold : int.MaxValue;
        }

        #endregion

        #region Hooks

        private bool IsNpc(BasePlayer player)
        {
            return player.IsNpc || !player.userID.IsSteamId();
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null) return;

            // Get killer
            BasePlayer killer = info?.InitiatorPlayer;

            // NPC died — only give XP to killer if test mode is on
            if (IsNpc(victim))
            {
                if (!_testMode || killer == null || IsNpc(killer)) return;

                var killerData = GetOrCreatePlayer(killer);
                killerData.Kills++;

                // Calculate and award XP (skip victim tracking for NPCs)
                int xpGained = CalculateXP(killerData, info);
                killerData.XP += xpGained;
                killerData.Dirty = true;

                bool leveledUp = CheckLevelUp(killerData);

                Message(killer, "XPGained", xpGained, killerData.XP, killerData.Level);
                if (leveledUp)
                {
                    if (killerData.Level >= _config.MaxLevel)
                        Message(killer, "MaxLevel");
                    else
                        Message(killer, "LevelUp", killerData.Level);
                    EquipKit(killer, killerData.Level);
                }

                SavePlayer(killerData);
                return;
            }

            // NPC killed a real player — don't give NPC any XP
            if (killer != null && IsNpc(killer))
            {
                // Still track the victim's death
                var victimData = GetOrCreatePlayer(victim);
                victimData.Deaths++;
                victimData.Dirty = true;
                SavePlayer(victimData);
                return;
            }

            // Track victim death
            var victimDataNormal = GetOrCreatePlayer(victim);
            victimDataNormal.Deaths++;
            victimDataNormal.Dirty = true;
            SavePlayer(victimDataNormal);

            if (killer == null || killer == victim) return;
            if (killer.userID == victim.userID) return;

            // Track killer stats
            var killerData2 = GetOrCreatePlayer(killer);
            killerData2.Kills++;

            // Calculate and award XP
            int xpEarned = CalculateXP(killerData2, info);
            killerData2.XP += xpEarned;
            killerData2.Dirty = true;

            // Check level up
            bool didLevelUp = CheckLevelUp(killerData2);

            // Notify killer
            Message(killer, "XPGained", xpEarned, killerData2.XP, killerData2.Level);

            if (didLevelUp)
            {
                if (killerData2.Level >= _config.MaxLevel)
                    Message(killer, "MaxLevel");
                else
                    Message(killer, "LevelUp", killerData2.Level);

                EquipKit(killer, killerData2.Level);
            }

            SavePlayer(killerData2);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;

            var data = GetOrCreatePlayer(player);
            timer.Once(0.5f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    EquipKit(player, data.Level);
                    int nextXP = GetXPForNextLevel(data.Level);
                    string xpDisplay = nextXP == int.MaxValue ? $"{data.XP} (MAX)" : $"{data.XP}/{nextXP}";
                    Message(player, "Stats", data.Level, xpDisplay, "", data.Kills, data.Deaths, data.KDRatio, data.Headshots);
                }
            });
        }

        private void OnNewSave(string filename)
        {
            if (_config.WipeOnNewSave)
            {
                Puts("New map save detected — wiping Gun Game data...");
                WipeDatabase();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            if (_playerCache.TryGetValue(player.userID, out PlayerData data))
                SavePlayer(data);
        }

        #endregion

        #region Kit Integration

        private void EquipKit(BasePlayer player, int level)
        {
            if (Kits == null)
            {
                Message(player, "KitsNotLoaded");
                return;
            }

            // Strip current inventory
            player.inventory.Strip();

            string kitName = $"{_config.KitPrefix}{level}";
            object result = Kits.Call("GiveKit", player, kitName);

            if (result is string errorMsg)
            {
                Message(player, "KitNotFound", kitName);
                PrintWarning($"Failed to give kit '{kitName}' to {player.displayName}: {errorMsg}");
            }
        }

        #endregion

        #region Test NPC Spawning

        private const string HumanPrefab = "assets/rust.ai/agents/npcplayer/humannpc/humanplayer.prefab";

        private void SpawnTestNPCs(BasePlayer player, int count)
        {
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                // Spread NPCs in a semicircle in front of the player
                float angle = (i - count / 2f) * 30f;
                Vector3 forward = Quaternion.Euler(0, angle, 0) * player.eyes.HeadForward();
                forward.y = 0;
                forward.Normalize();
                Vector3 spawnPos = player.transform.position + forward * 8f;

                // Raycast down to find ground
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out hit, 20f, Rust.Layers.Solid))
                    spawnPos = hit.point;

                var entity = GameManager.server.CreateEntity(HumanPrefab, spawnPos, Quaternion.LookRotation(-forward));
                if (entity == null) continue;

                var npc = entity as HumanNPC;
                if (npc == null)
                {
                    entity.Kill();
                    continue;
                }

                npc.Spawn();

                // Enable hostile AI so it fights back
                npc.SetAimDirection((-forward).normalized);

                // Assign a random level kit
                int randomLevel = _rng.Next(1, _config.MaxLevel + 1);
                timer.Once(0.5f, () =>
                {
                    if (npc == null || npc.IsDestroyed) return;

                    npc.inventory.Strip();

                    if (Kits != null)
                    {
                        string kitName = $"{_config.KitPrefix}{randomLevel}";
                        Kits.Call("GiveKit", npc, kitName);
                    }

                    // Set display name to show level
                    npc.displayName = $"Bot Lv.{randomLevel}";

                    // Equip held item so NPC uses it
                    var belt = npc.inventory.containerBelt;
                    if (belt != null)
                    {
                        var firstItem = belt.GetSlot(0);
                        if (firstItem != null)
                        {
                            npc.UpdateActiveItem(firstItem.uid);
                        }
                    }

                    npc.SendNetworkUpdateImmediate();
                });

                _spawnedNPCs.Add(npc);
                spawned++;
            }

            if (spawned > 0)
            {
                // Auto-enable test mode so kills grant XP
                if (!_testMode)
                    _testMode = true;

                Message(player, "SpawnedNPCs", spawned);
            }
            else
            {
                Message(player, "SpawnFailed");
            }
        }

        private void DespawnTestNPCs(BasePlayer player)
        {
            int removed = CleanupTestNPCs();

            if (removed > 0)
                Message(player, "DespawnedNPCs", removed);
            else
                Message(player, "NoNPCs");
        }

        private int CleanupTestNPCs()
        {
            int removed = 0;
            foreach (var entity in _spawnedNPCs)
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                    removed++;
                }
            }
            _spawnedNPCs.Clear();
            return removed;
        }

        #endregion

        #region Commands

        [ChatCommand("gg")]
        private void CmdGunGame(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ShowStats(player, player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "top":
                    ShowLeaderboard(player);
                    break;

                case "stats":
                    if (!HasAdmin(player)) return;
                    if (args.Length < 2)
                    {
                        Message(player, "Usage");
                        return;
                    }
                    var target = FindPlayer(args[1]);
                    if (target == null)
                    {
                        Message(player, "PlayerNotFound");
                        return;
                    }
                    ShowStatsOther(player, target);
                    break;

                case "setlevel":
                    if (!HasAdmin(player)) return;
                    if (args.Length < 3)
                    {
                        Message(player, "Usage");
                        return;
                    }
                    CmdSetLevel(player, args[1], args[2]);
                    break;

                case "wipe":
                    if (!HasAdmin(player)) return;
                    if (args.Length >= 2 && args[1].ToLower() == "confirm")
                    {
                        WipeDatabase();

                        // Re-equip all online players with level 1 kit
                        foreach (var bp in BasePlayer.activePlayerList)
                        {
                            var data = GetOrCreatePlayer(bp);
                            EquipKit(bp, data.Level);
                        }

                        Message(player, "WipeComplete");
                    }
                    else
                    {
                        Message(player, "WipeConfirm");
                    }
                    break;

                case "testmode":
                    if (!HasAdmin(player)) return;
                    _testMode = !_testMode;
                    Message(player, _testMode ? "TestModeOn" : "TestModeOff");
                    break;

                case "spawn":
                    if (!HasAdmin(player)) return;
                    int spawnCount = 1;
                    if (args.Length >= 2) int.TryParse(args[1], out spawnCount);
                    spawnCount = Math.Max(1, Math.Min(spawnCount, 10));
                    SpawnTestNPCs(player, spawnCount);
                    break;

                case "despawn":
                    if (!HasAdmin(player)) return;
                    DespawnTestNPCs(player);
                    break;

                case "help":
                    Message(player, "Usage");
                    break;

                default:
                    Message(player, "Usage");
                    break;
            }
        }

        #endregion

        #region Command Helpers

        private void ShowStats(BasePlayer player, BasePlayer target)
        {
            var data = GetOrCreatePlayer(target);
            int nextXP = GetXPForNextLevel(data.Level);
            string xpDisplay = nextXP == int.MaxValue ? $"{data.XP} (MAX)" : $"{data.XP}/{nextXP}";
            Message(player, "Stats", data.Level, xpDisplay, "", data.Kills, data.Deaths, data.KDRatio, data.Headshots);
        }

        private void ShowStatsOther(BasePlayer player, PlayerData target)
        {
            Message(player, "StatsOther", target.DisplayName, target.Level, target.XP, target.Kills, target.Deaths, target.KDRatio, target.Headshots);
        }

        private void ShowLeaderboard(BasePlayer player)
        {
            if (_playerCache.Count == 0)
            {
                Message(player, "TopEmpty");
                return;
            }

            var top = _playerCache.Values
                .OrderByDescending(p => p.Level)
                .ThenByDescending(p => p.XP)
                .ThenByDescending(p => p.Kills)
                .Take(_config.TopListSize)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(Lang("TopHeader", player));

            for (int i = 0; i < top.Count; i++)
            {
                var p = top[i];
                sb.AppendLine(string.Format(Lang("TopEntry", player), i + 1, p.DisplayName, p.Level, p.XP, p.Kills, p.KDRatio));
            }

            player.ChatMessage($"{_config.ChatPrefix}\n{sb}");
        }

        private void CmdSetLevel(BasePlayer player, string targetName, string levelStr)
        {
            if (!int.TryParse(levelStr, out int level) || level < 1 || level > _config.MaxLevel)
            {
                Message(player, "InvalidLevel", _config.MaxLevel);
                return;
            }

            // Try to find online player first
            BasePlayer targetPlayer = FindOnlinePlayer(targetName);
            PlayerData targetData = null;

            if (targetPlayer != null)
            {
                targetData = GetOrCreatePlayer(targetPlayer);
            }
            else
            {
                // Search cache for offline player
                targetData = _playerCache.Values.FirstOrDefault(p =>
                    p.DisplayName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (targetData == null)
            {
                Message(player, "PlayerNotFound");
                return;
            }

            targetData.Level = level;

            // Reset XP to the threshold of the new level
            if (_config.LevelXPThresholds.TryGetValue(level, out int threshold))
                targetData.XP = threshold;
            else if (level == 1)
                targetData.XP = 0;

            targetData.Dirty = true;
            SavePlayer(targetData);

            // Equip kit if online
            if (targetPlayer != null && targetPlayer.IsConnected)
                EquipKit(targetPlayer, level);

            Message(player, "SetLevel", targetData.DisplayName, level);
        }

        private bool HasAdmin(BasePlayer player)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermAdmin))
                return true;

            Message(player, "NoPermission");
            return false;
        }

        private BasePlayer FindOnlinePlayer(string nameOrId)
        {
            if (ulong.TryParse(nameOrId, out ulong steamId))
            {
                var found = BasePlayer.FindByID(steamId);
                if (found != null) return found;
            }

            return BasePlayer.activePlayerList.FirstOrDefault(p =>
                p.displayName.IndexOf(nameOrId, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private PlayerData FindPlayer(string nameOrId)
        {
            // Try online first
            var online = FindOnlinePlayer(nameOrId);
            if (online != null)
                return GetOrCreatePlayer(online);

            // Try steam ID in cache
            if (ulong.TryParse(nameOrId, out ulong steamId) && _playerCache.TryGetValue(steamId, out PlayerData data))
                return data;

            // Search by name in cache
            return _playerCache.Values.FirstOrDefault(p =>
                p.DisplayName.IndexOf(nameOrId, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion
    }
}
