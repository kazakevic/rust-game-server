using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
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
        private Plugin Kits, KillFeed, BountySystem, SpawnProtection, GunGameShop;

        private readonly Core.SQLite.Libraries.SQLite _sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
        private Connection _db;

        private Dictionary<ulong, PlayerData> _playerCache = new Dictionary<ulong, PlayerData>();

        // Track damage dealt to players for assist XP
        private Dictionary<ulong, Dictionary<ulong, float>> _damageTracker = new Dictionary<ulong, Dictionary<ulong, float>>();

        // Track who last killed each player for revenge kills
        private Dictionary<ulong, ulong> _lastKilledBy = new Dictionary<ulong, ulong>();

        // Track whether first blood has been claimed this session
        private bool _firstBloodClaimed = false;

        private const string PermAdmin = "gungame.admin";

        // CUI panel names
        private const string CUI_XPBar = "GunGame_XPBar";
        private const string CUI_XPGainPopup = "GunGame_XPGain";
        private const string CUI_LevelUpBanner = "GunGame_LevelUp";
        private const string CUI_ScreenFlash = "GunGame_Flash";
        private const string CUI_StatsBoard = "GunGame_Stats";
        private const string CUI_StreakPopup = "GunGame_Streak";
        private const string CUI_PrestigeBanner = "GunGame_Prestige";

        // Track active popup timers per player so we can cancel/extend
        private Dictionary<ulong, Timer> _xpPopupTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> _levelUpTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> _flashTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> _streakTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> _prestigeTimers = new Dictionary<ulong, Timer>();

        // Track players who typed /prestige and need to confirm
        private HashSet<ulong> _prestigePending = new HashSet<ulong>();

        // Track cumulative XP gain within rapid kills
        private Dictionary<ulong, int> _pendingXPDisplay = new Dictionary<ulong, int>();

        // Auto-save timer
        private Timer _autoSaveTimer;

        // Colors
        private const string ColorBg = "0.1 0.1 0.1 0.85";
        private const string ColorBgDark = "0.05 0.05 0.05 0.9";
        private const string ColorBarEmpty = "0.2 0.2 0.2 0.8";
        private const string ColorBarFill = "1.0 0.4 0.0 0.9";        // orange
        private const string ColorBarFillMax = "1.0 0.84 0.0 0.9";    // gold for max level
        private const string ColorAccent = "1.0 0.4 0.0 1.0";         // orange accent
        private const string ColorGold = "1.0 0.84 0.0 1.0";          // gold
        private const string ColorWhite = "1.0 1.0 1.0 1.0";
        private const string ColorWhiteSoft = "1.0 1.0 1.0 0.7";
        private const string ColorGreen = "0.0 1.0 0.3 1.0";
        private const string ColorLevelUp = "1.0 0.84 0.0 1.0";
        private const string ColorFlash = "1.0 0.84 0.0 0.15";

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("XPPerKill")]
            public int XPPerKill { get; set; } = 50;

            [JsonProperty("HeadshotBonusXP")]
            public int HeadshotBonusXP { get; set; } = 25;

            [JsonProperty("DistanceBonusXPPer50m")]
            public int DistanceBonusXPPer50m { get; set; } = 15;

            [JsonProperty("XPPerAnimalKill")]
            public int XPPerAnimalKill { get; set; } = 10;

            [JsonProperty("XPPerNPCKill")]
            public int XPPerNPCKill { get; set; } = 20;

            [JsonProperty("DifficultyMultiplier (scales XP earned: 0.5=hard, 1.0=normal, 2.0=easy)")]
            public float DifficultyMultiplier { get; set; } = 1.0f;

            [JsonProperty("KitPrefix")]
            public string KitPrefix { get; set; } = "level_";

            [JsonProperty("MaxLevel")]
            public int MaxLevel { get; set; } = 5;

            [JsonProperty("LevelXPThresholds")]
            public Dictionary<int, int> LevelXPThresholds { get; set; } = new Dictionary<int, int>
            {
                [2] = 500,
                [3] = 1200,
                [4] = 2200,
                [5] = 3500
            };

            [JsonProperty("WipeOnNewSave")]
            public bool WipeOnNewSave { get; set; } = true;

            [JsonProperty("ChatPrefix")]
            public string ChatPrefix { get; set; } = "<color=#ff6600>[GunGame]</color>";

            [JsonProperty("TopListSize")]
            public int TopListSize { get; set; } = 10;

            [JsonProperty("TestMode")]
            public bool TestMode { get; set; } = false;

            [JsonProperty("KillRewardItemShortname")]
            public string KillRewardItemShortname { get; set; } = "blood";

            [JsonProperty("KillRewardMinAmount")]
            public int KillRewardMinAmount { get; set; } = 1;

            [JsonProperty("KillRewardMaxAmount")]
            public int KillRewardMaxAmount { get; set; } = 5;

            [JsonProperty("HealOnKill")]
            public int HealOnKill { get; set; } = 40;

            [JsonProperty("RefillAmmoOnKill")]
            public bool RefillAmmoOnKill { get; set; } = true;

            [JsonProperty("XPLossOnDeathPercent")]
            public float XPLossOnDeathPercent { get; set; } = 0.1f;

            [JsonProperty("AssistXP")]
            public int AssistXP { get; set; } = 20;

            [JsonProperty("AssistDamageThreshold")]
            public float AssistDamageThreshold { get; set; } = 0.3f;

            [JsonProperty("RevengeKillBonusXP")]
            public int RevengeKillBonusXP { get; set; } = 30;

            [JsonProperty("FirstBloodBonusXP")]
            public int FirstBloodBonusXP { get; set; } = 100;

            [JsonProperty("UnderdogBonusPerLevel")]
            public float UnderdogBonusPerLevel { get; set; } = 0.2f;

            [JsonProperty("AutoSaveIntervalSeconds")]
            public int AutoSaveIntervalSeconds { get; set; } = 300;

            [JsonProperty("KillStreakRewards")]
            public List<KillStreakReward> KillStreakRewards { get; set; } = new List<KillStreakReward>
            {
                new KillStreakReward { Streak = 3, Title = "KILLING SPREE", XPMultiplier = 1.5f, BroadcastToServer = false },
                new KillStreakReward { Streak = 5, Title = "RAMPAGE", XPMultiplier = 1.5f, RewardItemShortname = "supply.signal", RewardItemAmount = 1, BroadcastToServer = true },
                new KillStreakReward { Streak = 7, Title = "UNSTOPPABLE", XPMultiplier = 2.0f, BroadcastToServer = true },
                new KillStreakReward { Streak = 10, Title = "GODLIKE", XPMultiplier = 2.0f, RewardItemShortname = "ammo.rocket.basic", RewardItemAmount = 1, BroadcastToServer = true },
            };

            [JsonProperty("PrestigeEnabled")]
            public bool PrestigeEnabled { get; set; } = true;

            [JsonProperty("MaxPrestige")]
            public int MaxPrestige { get; set; } = 10;

            [JsonProperty("PrestigeXPBonusPercent")]
            public float PrestigeXPBonusPercent { get; set; } = 0.05f;

            [JsonProperty("PrestigeCurrencyReward")]
            public int PrestigeCurrencyReward { get; set; } = 50;

            [JsonProperty("PrestigeTiers")]
            public List<PrestigeTier> PrestigeTiers { get; set; } = new List<PrestigeTier>
            {
                new PrestigeTier { MinPrestige = 1, Title = "Bronze", Color = "0.8 0.5 0.2 1.0", Symbol = "I" },
                new PrestigeTier { MinPrestige = 2, Title = "Silver", Color = "0.8 0.8 0.8 1.0", Symbol = "II" },
                new PrestigeTier { MinPrestige = 3, Title = "Gold", Color = "1.0 0.84 0.0 1.0", Symbol = "III" },
                new PrestigeTier { MinPrestige = 5, Title = "Platinum", Color = "0.4 0.85 0.9 1.0", Symbol = "V" },
                new PrestigeTier { MinPrestige = 7, Title = "Diamond", Color = "0.7 0.5 1.0 1.0", Symbol = "VII" },
                new PrestigeTier { MinPrestige = 10, Title = "Master", Color = "1.0 0.2 0.2 1.0", Symbol = "X" },
            };

        }

        private class PrestigeTier
        {
            [JsonProperty("MinPrestige")]
            public int MinPrestige { get; set; }

            [JsonProperty("Title")]
            public string Title { get; set; }

            [JsonProperty("Color")]
            public string Color { get; set; }

            [JsonProperty("Symbol")]
            public string Symbol { get; set; }
        }

        private class KillStreakReward
        {
            [JsonProperty("Streak")]
            public int Streak { get; set; }

            [JsonProperty("Title")]
            public string Title { get; set; }

            [JsonProperty("XPMultiplier")]
            public float XPMultiplier { get; set; } = 1f;

            [JsonProperty("RewardItemShortname")]
            public string RewardItemShortname { get; set; }

            [JsonProperty("RewardItemAmount")]
            public int RewardItemAmount { get; set; }

            [JsonProperty("BroadcastToServer")]
            public bool BroadcastToServer { get; set; }
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
            public int AnimalKills { get; set; }
            public int NPCKills { get; set; }
            public int XP { get; set; }
            public int Level { get; set; } = 1;
            public int CurrentStreak { get; set; }
            public int BestStreak { get; set; }
            public int Prestige { get; set; }
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
                ["Stats"] = "<color=#00ffff>--- Your Stats ---</color>\nLevel: {0} | XP: {1}/{2}\nKills: {3} | Deaths: {4} | K/D: {5}\nHeadshots: {6} | Animals: {7} | NPCs: {8}",
                ["StatsOther"] = "<color=#00ffff>--- {0}'s Stats ---</color>\nLevel: {1} | XP: {2}\nKills: {3} | Deaths: {4} | K/D: {5}\nHeadshots: {6} | Animals: {7} | NPCs: {8}",
                ["TopHeader"] = "<color=#00ffff>--- Gun Game Leaderboard ---</color>",
                ["TopEntry"] = "#{0} <color=#ffff00>{1}</color> — Level {2} | XP: {3} | Kills: {4} | K/D: {5}",
                ["TopEmpty"] = "No player data yet.",
                ["WipeComplete"] = "<color=#ff0000>All Gun Game data has been wiped!</color>",
                ["WipeConfirm"] = "Type <color=#ff0000>/gg wipe confirm</color> to confirm data wipe.",
                ["SetLevel"] = "Set <color=#00ffff>{0}</color> to level <color=#ffff00>{1}</color>.",
                ["PlayerNotFound"] = "Player not found.",
                ["InvalidLevel"] = "Invalid level. Must be between 1 and {0}.",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["Usage"] = "<color=#00ffff>Usage:</color>\n/gg — View your stats\n/gg top — Leaderboard\n/prestige — Prestige at max level\n/gg stats <player> — View player stats (admin)\n/gg setlevel <player> <level> — Set level (admin)\n/gg wipe — Reset all data (admin)",
                ["KitNotFound"] = "<color=#ff0000>Kit '{0}' not found. Ask an admin to create it.</color>",
                ["KitsNotLoaded"] = "<color=#ff0000>Kits plugin is not loaded!</color>",
                ["AnimalKillXP"] = "You gained <color=#00ff00>+{0} XP</color> from an animal kill ({1} total) — Level {2}",
                ["NPCKillXP"] = "You gained <color=#00ff00>+{0} XP</color> from an NPC kill ({1} total) — Level {2}",
                ["StreakAnnounce"] = "<color=#ff6600>{0}</color> is on a <color=#ffff00>{1}</color> kill streak! ({2})",
                ["StreakEnded"] = "<color=#ff6600>{0}</color> ended <color=#ffff00>{1}</color>'s {2} kill streak!",
                ["XPLost"] = "You lost <color=#ff4444>-{0} XP</color> on death ({1} total) — Level {2}",
                ["AssistXP"] = "You gained <color=#00ff00>+{0} XP</color> (assist on {1}) — Level {2}",
                ["RevengeKill"] = "<color=#ff4444>REVENGE!</color> +{0} bonus XP for avenging your death!",
                ["FirstBlood"] = "<color=#ff0000>FIRST BLOOD!</color> <color=#ffff00>{0}</color> draws first blood and earns <color=#00ff00>+{1} XP</color>!",
                ["UnderdogBonus"] = "+{0} underdog bonus XP (level difference)",
                ["PrestigeUp"] = "<color=#ff00ff>PRESTIGE UP!</color> <color=#ffff00>{0}</color> is now <color=#00ffff>Prestige {1}</color> ({2})!",
                ["PrestigeConfirm"] = "Are you sure you want to prestige? This will <color=#ff4444>reset your level and XP to zero</color> but grant you <color=#00ffff>Prestige {0}</color> ({1}). Type <color=#ffff00>/prestige confirm</color> to proceed.",
                ["PrestigeNotReady"] = "You must reach <color=#ffff00>max level ({0})</color> before you can prestige.",
                ["PrestigeMaxed"] = "You have reached the <color=#ff00ff>maximum prestige level</color> ({0})!",
                ["PrestigeDisabled"] = "Prestige is not enabled on this server.",
                ["PrestigeBonus"] = "Prestige {0} — <color=#00ff00>+{1}% XP bonus</color>",
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

        private string BuildProgressBar(int currentXP, int nextLevelXP, int barLength = 20)
        {
            if (nextLevelXP == int.MaxValue)
                return "<color=#ffff00>[████████████████████]</color> MAX";

            float progress = Mathf.Clamp01((float)currentXP / nextLevelXP);
            int filled = Mathf.RoundToInt(progress * barLength);
            int empty = barLength - filled;

            string filledBar = new string('█', filled);
            string emptyBar = new string('░', empty);
            int percent = Mathf.RoundToInt(progress * 100f);

            return $"<color=#00ff00>{filledBar}</color><color=#555555>{emptyBar}</color> {percent}% ({currentXP}/{nextLevelXP})";
        }

        private void ShowXPGain(BasePlayer player, PlayerData data, int xpGained, string sourceKey)
        {
            // Update persistent XP bar
            CreateXPBar(player, data);

            // Show floating XP popup (accumulates for rapid kills)
            ShowXPGainPopup(player, xpGained);

            // Play a subtle sound on XP gain
            Effect.server.Run("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player.transform.position);
        }

        private void ShowLevelUp(BasePlayer player, PlayerData data)
        {
            // Update the XP bar first
            CreateXPBar(player, data);

            // Show level up banner
            ShowLevelUpBanner(player, data);

            // Screen flash effect
            ShowScreenFlash(player);

            // Play level up sound & gesture
            Effect.server.Run("assets/bundled/prefabs/fx/gestures/drink_raise.prefab", player.transform.position);
            Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", player.transform.position);
            player.SignalBroadcast(BaseEntity.Signal.Gesture, "victory");

            EquipKit(player, data.Level);

            // Restore full HP, food, and water
            player.health = player.MaxHealth();
            player.metabolism.calories.SetValue(player.metabolism.calories.max);
            player.metabolism.hydration.SetValue(player.metabolism.hydration.max);
        }

        #endregion

        #region CUI - Persistent XP Bar

        private void CreateXPBar(BasePlayer player, PlayerData data)
        {
            // Destroy existing bar and streak indicator
            CuiHelper.DestroyUi(player, CUI_XPBar + "_Streak");
            CuiHelper.DestroyUi(player, CUI_XPBar);

            int nextXP = GetXPForNextLevel(data.Level);
            int currentLevelXP = GetXPForCurrentLevel(data.Level);
            bool isMax = nextXP == int.MaxValue;
            int xpIntoLevel = data.XP - currentLevelXP;
            int xpNeeded = nextXP - currentLevelXP;
            float progress = isMax ? 1f : Mathf.Clamp01((float)xpIntoLevel / xpNeeded);
            int percent = Mathf.RoundToInt(progress * 100f);

            string xpText = isMax ? "MAX LEVEL" : $"{xpIntoLevel} / {xpNeeded} XP";
            string barColor = isMax ? ColorBarFillMax : ColorBarFill;
            string levelColor = isMax ? ColorGold : ColorAccent;

            // Prestige info
            bool hasPrestige = data.Prestige > 0;
            var prestigeTier = hasPrestige ? GetPrestigeTier(data.Prestige) : null;
            string prestigeBadgeColor = prestigeTier?.Color ?? "1.0 0.0 1.0 1.0";

            var elements = new CuiElementContainer();

            // Main container — wider if prestiged to fit badge
            string barMaxX = hasPrestige ? "0.2" : "0.17";
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorBg, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.005 0.025", AnchorMax = $"{barMaxX} 0.052" },
                CursorEnabled = false
            }, "Hud", CUI_XPBar);

            // Prestige badge (far left, only if prestiged)
            float lvlAnchorStart = 0f;
            if (hasPrestige)
            {
                elements.Add(new CuiPanel
                {
                    Image = { Color = prestigeBadgeColor },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.08 1" }
                }, CUI_XPBar, CUI_XPBar + "_PBg");

                elements.Add(new CuiLabel
                {
                    Text = { Text = $"P{data.Prestige}", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.05 0.05 0.05 1.0", Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, CUI_XPBar + "_PBg");

                lvlAnchorStart = 0.085f;
            }

            // Level badge
            string lvlEnd = hasPrestige ? "0.18" : "0.12";
            elements.Add(new CuiPanel
            {
                Image = { Color = levelColor },
                RectTransform = { AnchorMin = $"{lvlAnchorStart} 0", AnchorMax = $"{lvlEnd} 1" }
            }, CUI_XPBar, CUI_XPBar + "_LvlBg");

            elements.Add(new CuiLabel
            {
                Text = { Text = isMax ? $"LV {data.Level}" : $"LV {data.Level}/{_config.MaxLevel}", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.05 0.05 0.05 1.0", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, CUI_XPBar + "_LvlBg");

            // Bar background (middle area) — shift right if prestiged
            float barStart = hasPrestige ? 0.195f : 0.135f;
            float barEnd = 0.86f;
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorBarEmpty },
                RectTransform = { AnchorMin = $"{barStart} 0.2", AnchorMax = $"{barEnd} 0.8" }
            }, CUI_XPBar, CUI_XPBar + "_BarBg");

            // Bar fill
            string fillMax = (barStart + progress * (barEnd - barStart)).ToString("F4");
            elements.Add(new CuiPanel
            {
                Image = { Color = barColor },
                RectTransform = { AnchorMin = $"{barStart} 0.2", AnchorMax = $"{fillMax} 0.8" }
            }, CUI_XPBar, CUI_XPBar + "_BarFill");

            // Bar glow overlay (subtle shine on the fill)
            if (progress > 0.02f)
            {
                elements.Add(new CuiPanel
                {
                    Image = { Color = "1.0 1.0 1.0 0.08" },
                    RectTransform = { AnchorMin = $"{barStart} 0.55", AnchorMax = $"{fillMax} 0.8" }
                }, CUI_XPBar);
            }

            // XP text (centered on bar)
            elements.Add(new CuiLabel
            {
                Text = { Text = xpText, FontSize = 7, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = $"{barStart} 0", AnchorMax = $"{barEnd} 1" }
            }, CUI_XPBar);

            // Percentage text (right side)
            elements.Add(new CuiLabel
            {
                Text = { Text = isMax ? "★" : $"{percent}%", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = isMax ? ColorGold : ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.86 0", AnchorMax = "1 1" }
            }, CUI_XPBar);

            // Streak indicator (shows when streak >= 2)
            if (data.CurrentStreak >= 2)
            {
                string streakText = $"x{data.CurrentStreak}";
                string streakCol = data.CurrentStreak >= 10 ? "1.0 0.2 0.2 1.0" : data.CurrentStreak >= 5 ? "1.0 0.5 0.0 1.0" : "1.0 0.85 0.0 1.0";
                string streakMinX = hasPrestige ? "0.205" : "0.175";
                string streakMaxX = hasPrestige ? "0.25" : "0.22";

                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 0.85" },
                    RectTransform = { AnchorMin = $"{streakMinX} 0.025", AnchorMax = $"{streakMaxX} 0.052" },
                    CursorEnabled = false
                }, "Hud", CUI_XPBar + "_Streak");

                elements.Add(new CuiLabel
                {
                    Text = { Text = streakText, FontSize = 8, Align = TextAnchor.MiddleCenter, Color = streakCol, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, CUI_XPBar + "_Streak");
            }

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyXPBar(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_XPBar + "_Streak");
            CuiHelper.DestroyUi(player, CUI_XPBar);
        }

        #endregion

        #region CUI - XP Gain Popup

        private void ShowXPGainPopup(BasePlayer player, int xpGained)
        {
            ulong id = player.userID;

            // Accumulate XP for rapid kills
            if (!_pendingXPDisplay.ContainsKey(id))
                _pendingXPDisplay[id] = 0;
            _pendingXPDisplay[id] += xpGained;

            int totalXP = _pendingXPDisplay[id];

            // Cancel existing popup timer
            if (_xpPopupTimers.TryGetValue(id, out Timer existing))
                existing?.Destroy();

            // Destroy old popup
            CuiHelper.DestroyUi(player, CUI_XPGainPopup);

            var elements = new CuiElementContainer();

            // Popup container — above the XP bar in bottom-left
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.005 0.058", AnchorMax = "0.17 0.095" },
                CursorEnabled = false,
                FadeOut = 0.8f
            }, "Hud", CUI_XPGainPopup);

            // Glow background
            elements.Add(new CuiPanel
            {
                Image = { Color = "1.0 0.4 0.0 0.15" },
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 1" },
                FadeOut = 0.8f
            }, CUI_XPGainPopup);

            // XP gain text
            string text = totalXP == xpGained ? $"+{xpGained} XP" : $"+{xpGained} XP  (+{totalXP} combo)";
            elements.Add(new CuiLabel
            {
                Text = { Text = text, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = ColorGreen, Font = "robotocondensed-bold.ttf", FadeIn = 0.1f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                FadeOut = 0.8f
            }, CUI_XPGainPopup);

            CuiHelper.AddUi(player, elements);

            // Auto-remove after 2 seconds and reset accumulator
            _xpPopupTimers[id] = timer.Once(2f, () =>
            {
                if (player != null && player.IsConnected)
                    CuiHelper.DestroyUi(player, CUI_XPGainPopup);
                _pendingXPDisplay.Remove(id);
                _xpPopupTimers.Remove(id);
            });
        }

        #endregion

        #region CUI - Level Up Banner

        private void ShowLevelUpBanner(BasePlayer player, PlayerData data)
        {
            ulong id = player.userID;

            if (_levelUpTimers.TryGetValue(id, out Timer existing))
                existing?.Destroy();

            CuiHelper.DestroyUi(player, CUI_LevelUpBanner);

            var elements = new CuiElementContainer();

            bool isMax = data.Level >= _config.MaxLevel;
            bool canPrestige = isMax && _config.PrestigeEnabled && data.Prestige < _config.MaxPrestige;
            string title = isMax ? "★ MAX LEVEL REACHED ★" : $"LEVEL UP!";
            string subtitle = isMax
                ? (canPrestige ? "Type /prestige to advance!" : "You have mastered Gun Game!")
                : $"You are now Level {data.Level}";

            // Full-width banner near top of screen
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.05 0.92", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.25 0.78", AnchorMax = "0.75 0.9" },
                CursorEnabled = false,
                FadeOut = 1.5f
            }, "Hud", CUI_LevelUpBanner);

            // Top accent line
            elements.Add(new CuiPanel
            {
                Image = { Color = isMax ? ColorGold : ColorAccent },
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" },
                FadeOut = 1.5f
            }, CUI_LevelUpBanner);

            // Bottom accent line
            elements.Add(new CuiPanel
            {
                Image = { Color = isMax ? ColorGold : ColorAccent },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.08" },
                FadeOut = 1.5f
            }, CUI_LevelUpBanner);

            // Level number (big, left side)
            elements.Add(new CuiLabel
            {
                Text = { Text = data.Level.ToString(), FontSize = 42, Align = TextAnchor.MiddleCenter, Color = isMax ? ColorGold : ColorAccent, Font = "robotocondensed-bold.ttf", FadeIn = 0.15f },
                RectTransform = { AnchorMin = "0 0.05", AnchorMax = "0.2 0.92" },
                FadeOut = 1.5f
            }, CUI_LevelUpBanner);

            // Title text
            elements.Add(new CuiLabel
            {
                Text = { Text = title, FontSize = 22, Align = TextAnchor.MiddleLeft, Color = isMax ? ColorGold : ColorLevelUp, Font = "robotocondensed-bold.ttf", FadeIn = 0.15f },
                RectTransform = { AnchorMin = "0.22 0.45", AnchorMax = "0.95 0.92" },
                FadeOut = 1.5f
            }, CUI_LevelUpBanner);

            // Subtitle text
            elements.Add(new CuiLabel
            {
                Text = { Text = subtitle, FontSize = 14, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0.22 0.08", AnchorMax = "0.95 0.5" },
                FadeOut = 1.5f
            }, CUI_LevelUpBanner);

            CuiHelper.AddUi(player, elements);

            // Auto-remove after 4 seconds
            _levelUpTimers[id] = timer.Once(4f, () =>
            {
                if (player != null && player.IsConnected)
                    CuiHelper.DestroyUi(player, CUI_LevelUpBanner);
                _levelUpTimers.Remove(id);
            });
        }

        #endregion

        #region CUI - Screen Flash

        private void ShowScreenFlash(BasePlayer player)
        {
            ulong id = player.userID;

            if (_flashTimers.TryGetValue(id, out Timer existing))
                existing?.Destroy();

            CuiHelper.DestroyUi(player, CUI_ScreenFlash);

            var elements = new CuiElementContainer();

            // Full screen overlay flash
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorFlash },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = false,
                FadeOut = 1.0f
            }, "Overlay", CUI_ScreenFlash);

            CuiHelper.AddUi(player, elements);

            _flashTimers[id] = timer.Once(1.2f, () =>
            {
                if (player != null && player.IsConnected)
                    CuiHelper.DestroyUi(player, CUI_ScreenFlash);
                _flashTimers.Remove(id);
            });
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

            _firstBloodClaimed = false;
            _damageTracker.Clear();
            _lastKilledBy.Clear();

            OpenDatabase();

            // Recalculate levels and refresh UI for all online players (handles plugin reload / config change)
            timer.Once(1f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    var data = GetOrCreatePlayer(player);
                    RecalculateLevel(data);
                    EquipKit(player, data.Level);
                    CreateXPBar(player, data);
                    BountySystem?.Call("RefreshBountyUI", player);
                }
            });

            // Auto-save timer
            if (_config.AutoSaveIntervalSeconds > 0)
                _autoSaveTimer = timer.Every(_config.AutoSaveIntervalSeconds, () => SaveAllPlayers());
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            // Slight delay to let player fully load in
            timer.Once(2f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    var data = GetOrCreatePlayer(player);
                    CreateXPBar(player, data);
                }
            });
        }

        private void Unload()
        {
            // Destroy all UI for all online players
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, CUI_XPBar);
                CuiHelper.DestroyUi(player, CUI_XPGainPopup);
                CuiHelper.DestroyUi(player, CUI_LevelUpBanner);
                CuiHelper.DestroyUi(player, CUI_ScreenFlash);
                CuiHelper.DestroyUi(player, CUI_StatsBoard);
                CuiHelper.DestroyUi(player, CUI_StreakPopup);
                CuiHelper.DestroyUi(player, CUI_PrestigeBanner);
            }

            // Cancel all timers
            foreach (var t in _xpPopupTimers.Values) t?.Destroy();
            foreach (var t in _levelUpTimers.Values) t?.Destroy();
            foreach (var t in _flashTimers.Values) t?.Destroy();
            foreach (var t in _streakTimers.Values) t?.Destroy();
            foreach (var t in _prestigeTimers.Values) t?.Destroy();
            _xpPopupTimers.Clear();
            _levelUpTimers.Clear();
            _flashTimers.Clear();
            _streakTimers.Clear();
            _prestigeTimers.Clear();
            _prestigePending.Clear();

            _autoSaveTimer?.Destroy();

            _damageTracker.Clear();
            _lastKilledBy.Clear();

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
                    animal_kills INTEGER NOT NULL DEFAULT 0,
                    npc_kills INTEGER NOT NULL DEFAULT 0,
                    xp INTEGER NOT NULL DEFAULT 0,
                    level INTEGER NOT NULL DEFAULT 1,
                    best_streak INTEGER NOT NULL DEFAULT 0,
                    prestige INTEGER NOT NULL DEFAULT 0,
                    last_updated TEXT NOT NULL DEFAULT (datetime('now'))
                );";

            _sqlite.ExecuteNonQuery(Sql.Builder.Append(createTable), _db);

            // Migrate: add columns if they don't exist
            _sqlite.Query(Sql.Builder.Append("PRAGMA table_info(player_stats);"), _db, list =>
            {
                if (list == null) return;
                var columns = new HashSet<string>();
                foreach (var row in list)
                    columns.Add(row["name"].ToString());

                if (!columns.Contains("animal_kills"))
                    _sqlite.ExecuteNonQuery(Sql.Builder.Append("ALTER TABLE player_stats ADD COLUMN animal_kills INTEGER NOT NULL DEFAULT 0;"), _db);
                if (!columns.Contains("npc_kills"))
                    _sqlite.ExecuteNonQuery(Sql.Builder.Append("ALTER TABLE player_stats ADD COLUMN npc_kills INTEGER NOT NULL DEFAULT 0;"), _db);
                if (!columns.Contains("best_streak"))
                    _sqlite.ExecuteNonQuery(Sql.Builder.Append("ALTER TABLE player_stats ADD COLUMN best_streak INTEGER NOT NULL DEFAULT 0;"), _db);
                if (!columns.Contains("prestige"))
                    _sqlite.ExecuteNonQuery(Sql.Builder.Append("ALTER TABLE player_stats ADD COLUMN prestige INTEGER NOT NULL DEFAULT 0;"), _db);
            });

            LoadAllPlayers();
        }

        private void CloseDatabase()
        {
            if (_db != null)
                _sqlite.CloseDb(_db);
        }

        private void LoadAllPlayers()
        {
            string query = "SELECT steam_id, display_name, kills, deaths, headshots, animal_kills, npc_kills, xp, level, best_streak, prestige FROM player_stats;";
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
                        AnimalKills = Convert.ToInt32(row["animal_kills"]),
                        NPCKills = Convert.ToInt32(row["npc_kills"]),
                        XP = Convert.ToInt32(row["xp"]),
                        Level = Convert.ToInt32(row["level"]),
                        BestStreak = Convert.ToInt32(row["best_streak"]),
                        Prestige = Convert.ToInt32(row["prestige"]),
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
                INSERT INTO player_stats (steam_id, display_name, kills, deaths, headshots, animal_kills, npc_kills, xp, level, best_streak, prestige, last_updated)
                VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, datetime('now'))
                ON CONFLICT(steam_id) DO UPDATE SET
                    display_name = @1,
                    kills = @2,
                    deaths = @3,
                    headshots = @4,
                    animal_kills = @5,
                    npc_kills = @6,
                    xp = @7,
                    level = @8,
                    best_streak = @9,
                    prestige = @10,
                    last_updated = datetime('now');";

            _sqlite.ExecuteNonQuery(
                Sql.Builder.Append(upsert,
                    data.SteamId.ToString(),
                    data.DisplayName,
                    data.Kills,
                    data.Deaths,
                    data.Headshots,
                    data.AnimalKills,
                    data.NPCKills,
                    data.XP,
                    data.Level,
                    data.BestStreak,
                    data.Prestige),
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

            xpGained = (int)(xpGained * _config.DifficultyMultiplier);

            // Apply prestige XP bonus
            if (killerData.Prestige > 0)
                xpGained = (int)(xpGained * GetPrestigeXPMultiplier(killerData.Prestige));

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

        private void RecalculateLevel(PlayerData data)
        {
            // Reset to level 1 and re-derive level from current XP and thresholds
            data.Level = 1;
            while (data.Level < _config.MaxLevel && data.XP >= GetXPForNextLevel(data.Level))
            {
                data.Level++;
            }
            // Cap level to MaxLevel
            if (data.Level > _config.MaxLevel)
                data.Level = _config.MaxLevel;
            data.Dirty = true;
        }

        private int GetXPForNextLevel(int currentLevel)
        {
            int nextLevel = currentLevel + 1;
            if (nextLevel > _config.MaxLevel) return int.MaxValue;
            if (!_config.LevelXPThresholds.TryGetValue(nextLevel, out int threshold)) return int.MaxValue;
            return threshold;
        }

        private int GetXPForCurrentLevel(int currentLevel)
        {
            if (currentLevel <= 1) return 0;
            if (!_config.LevelXPThresholds.TryGetValue(currentLevel, out int threshold)) return 0;
            return threshold;
        }

        private PrestigeTier GetPrestigeTier(int prestige)
        {
            if (prestige <= 0) return null;
            PrestigeTier best = null;
            foreach (var tier in _config.PrestigeTiers)
            {
                if (prestige >= tier.MinPrestige && (best == null || tier.MinPrestige > best.MinPrestige))
                    best = tier;
            }
            return best;
        }

        private float GetPrestigeXPMultiplier(int prestige)
        {
            return 1f + prestige * _config.PrestigeXPBonusPercent;
        }

        private string GetPrestigeTag(int prestige)
        {
            if (prestige <= 0) return "";
            var tier = GetPrestigeTier(prestige);
            if (tier == null) return $"P{prestige}";
            return $"P{prestige}";
        }

        #endregion

        #region Hooks

        private bool IsNpc(BasePlayer player)
        {
            return player.IsNpc || !player.userID.IsSteamId();
        }

        // Track damage dealt for assist XP
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null) return;
            var victim = info.HitEntity as BasePlayer;
            if (victim == null || IsNpc(attacker) || IsNpc(victim)) return;
            if (attacker.userID == victim.userID) return;

            float damage = info.damageTypes.Total();
            if (damage <= 0) return;

            if (!_damageTracker.ContainsKey(victim.userID))
                _damageTracker[victim.userID] = new Dictionary<ulong, float>();

            if (!_damageTracker[victim.userID].ContainsKey(attacker.userID))
                _damageTracker[victim.userID][attacker.userID] = 0f;

            _damageTracker[victim.userID][attacker.userID] += damage;
        }

        // HumanNPC hook — called when a HumanNPC bot is killed
        private void OnKillNPC(BasePlayer npc, HitInfo info)
        {
            if (npc == null || info == null) return;

            BasePlayer killer = info.InitiatorPlayer;
            if (killer == null || IsNpc(killer)) return;

            var killerData = GetOrCreatePlayer(killer);
            killerData.NPCKills++;

            int xpGained = _config.XPPerNPCKill;
            if (info.isHeadshot)
                xpGained += _config.HeadshotBonusXP;
            xpGained = (int)(xpGained * _config.DifficultyMultiplier);
            if (killerData.Prestige > 0)
                xpGained = (int)(xpGained * GetPrestigeXPMultiplier(killerData.Prestige));

            killerData.XP += xpGained;
            killerData.Dirty = true;

            bool leveledUp = CheckLevelUp(killerData);

            ShowXPGain(killer, killerData, xpGained, "NPCKillXP");
            if (leveledUp)
                ShowLevelUp(killer, killerData);

            if (_config.TestMode)
                GiveKillReward(killer);

            SavePlayer(killerData);
        }

        // Called when any entity dies — used to track animal kills
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (entity is BasePlayer) return; // Handled by OnPlayerDeath/OnKillNPC

            BasePlayer killer = info.InitiatorPlayer;
            if (killer == null || IsNpc(killer)) return;

            // Check if the entity is an animal (BaseAnimalNPC or derived types)
            if (!(entity is BaseAnimalNPC)) return;

            var killerData = GetOrCreatePlayer(killer);
            killerData.AnimalKills++;

            int xpGained = (int)(_config.XPPerAnimalKill * _config.DifficultyMultiplier);
            if (killerData.Prestige > 0)
                xpGained = (int)(xpGained * GetPrestigeXPMultiplier(killerData.Prestige));
            killerData.XP += xpGained;
            killerData.Dirty = true;

            bool leveledUp = CheckLevelUp(killerData);

            ShowXPGain(killer, killerData, xpGained, "AnimalKillXP");
            if (leveledUp)
                ShowLevelUp(killer, killerData);

            if (_config.TestMode)
                GiveKillReward(killer);

            SavePlayer(killerData);
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null) return;

            // Get killer
            BasePlayer killer = info?.InitiatorPlayer;

            // Skip NPC deaths — handled by OnKillNPC for HumanNPC bots
            if (IsNpc(victim)) return;

            // NPC killed a real player — don't give NPC any XP
            if (killer != null && IsNpc(killer))
            {
                var victimData = GetOrCreatePlayer(victim);
                victimData.Deaths++;
                victimData.CurrentStreak = 0;
                ApplyDeathXPPenalty(victim, victimData);
                victimData.Dirty = true;
                SavePlayer(victimData);
                return;
            }

            // Track victim death
            var victimDataNormal = GetOrCreatePlayer(victim);
            victimDataNormal.Deaths++;

            // Announce ended streak if significant
            if (victimDataNormal.CurrentStreak >= 3 && killer != null && killer != victim)
                PrintToChat($"{_config.ChatPrefix} {Lang("StreakEnded", null, killer.displayName, victim.displayName, victimDataNormal.CurrentStreak)}");

            victimDataNormal.CurrentStreak = 0;
            ApplyDeathXPPenalty(victim, victimDataNormal);
            victimDataNormal.Dirty = true;
            SavePlayer(victimDataNormal);

            // Remove spawn protection from victim (cleanup)
            SpawnProtection?.Call("RemoveProtection", victim);

            // Award assist XP to other players who dealt significant damage
            AwardAssistXP(victim.userID, killer?.userID ?? 0);

            // Clear damage tracking for the victim
            _damageTracker.Remove(victim.userID);

            if (killer == null || killer == victim) return;
            if (killer.userID == victim.userID) return;

            // Track revenge — record who killed the victim
            _lastKilledBy[victim.userID] = killer.userID;

            // Track killer stats
            var killerData2 = GetOrCreatePlayer(killer);
            killerData2.Kills++;
            killerData2.CurrentStreak++;
            if (killerData2.CurrentStreak > killerData2.BestStreak)
                killerData2.BestStreak = killerData2.CurrentStreak;

            // Calculate and award XP
            int xpEarned = CalculateXP(killerData2, info);

            // Bounty multiplier (from BountySystem plugin)
            if (BountySystem != null)
            {
                float bountyMult = (float)(BountySystem.Call("GetBountyMultiplier", victim.userID) ?? 1f);
                if (bountyMult > 1f)
                    xpEarned = (int)(xpEarned * bountyMult);
            }

            // Kill streak XP multiplier
            float streakMultiplier = GetStreakXPMultiplier(killerData2.CurrentStreak);
            if (streakMultiplier > 1f)
                xpEarned = (int)(xpEarned * streakMultiplier);

            // Underdog bonus — extra XP for killing higher-level players
            int underdogBonus = 0;
            if (_config.UnderdogBonusPerLevel > 0)
            {
                int levelDiff = victimDataNormal.Level - killerData2.Level;
                if (levelDiff > 0)
                {
                    underdogBonus = (int)(xpEarned * levelDiff * _config.UnderdogBonusPerLevel);
                    xpEarned += underdogBonus;
                }
            }

            // Revenge kill bonus
            int revengeBonus = 0;
            if (_config.RevengeKillBonusXP > 0 && _lastKilledBy.TryGetValue(killer.userID, out ulong lastKiller))
            {
                if (lastKiller == victim.userID)
                {
                    revengeBonus = _config.RevengeKillBonusXP;
                    xpEarned += revengeBonus;
                    _lastKilledBy.Remove(killer.userID);
                }
            }

            // First blood bonus
            int firstBloodBonus = 0;
            if (!_firstBloodClaimed && _config.FirstBloodBonusXP > 0)
            {
                _firstBloodClaimed = true;
                firstBloodBonus = _config.FirstBloodBonusXP;
                xpEarned += firstBloodBonus;
                PrintToChat($"{_config.ChatPrefix} {Lang("FirstBlood", null, killer.displayName, firstBloodBonus)}");
            }

            killerData2.XP += xpEarned;
            killerData2.Dirty = true;

            // Check level up
            bool didLevelUp = CheckLevelUp(killerData2);

            // Notify killer with progress bar
            ShowXPGain(killer, killerData2, xpEarned, "XPGained");

            // Show bonus notifications
            if (revengeBonus > 0)
                killer.ChatMessage($"{_config.ChatPrefix} {Lang("RevengeKill", killer, revengeBonus)}");
            if (underdogBonus > 0)
                killer.ChatMessage($"{_config.ChatPrefix} {Lang("UnderdogBonus", killer, underdogBonus)}");

            if (didLevelUp)
                ShowLevelUp(killer, killerData2);

            // Kill streak handling
            HandleKillStreak(killer, killerData2);

            // Heal on kill
            if (_config.HealOnKill > 0)
                killer.health = Math.Min(killer.health + _config.HealOnKill, killer.MaxHealth());

            // Ammo refill on kill
            if (_config.RefillAmmoOnKill)
                RefillActiveWeaponAmmo(killer);

            GiveKillReward(killer);

            // Kill feed (via KillFeed plugin)
            if (KillFeed != null)
            {
                string weaponName = info?.Weapon?.GetItem()?.info?.displayName?.english ?? "Unknown";
                float distance = info?.IsProjectile() == true ? info.ProjectileDistance : 0f;
                KillFeed.Call("AddEntry", killer.displayName, victim.displayName, weaponName, info?.isHeadshot == true, distance);
            }

            // Update bounty (via BountySystem plugin)
            UpdateBountyTarget();

            SavePlayer(killerData2);
        }

        private void ApplyDeathXPPenalty(BasePlayer player, PlayerData data)
        {
            if (_config.XPLossOnDeathPercent <= 0) return;

            int currentLevelXP = GetXPForCurrentLevel(data.Level);
            int nextLevelXP = GetXPForNextLevel(data.Level);
            if (nextLevelXP == int.MaxValue) return; // Don't penalize max level

            int xpNeeded = nextLevelXP - currentLevelXP;
            int xpLoss = (int)(xpNeeded * _config.XPLossOnDeathPercent);
            if (xpLoss <= 0) return;

            data.XP = Math.Max(data.XP - xpLoss, currentLevelXP);
            data.Dirty = true;

            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"{_config.ChatPrefix} {Lang("XPLost", player, xpLoss, data.XP, data.Level)}");
                CreateXPBar(player, data);
            }
        }

        private float GetStreakXPMultiplier(int streak)
        {
            float multiplier = 1f;
            foreach (var reward in _config.KillStreakRewards)
            {
                if (streak >= reward.Streak && reward.XPMultiplier > multiplier)
                    multiplier = reward.XPMultiplier;
            }
            return multiplier;
        }

        private void AwardAssistXP(ulong victimId, ulong killerId)
        {
            if (_config.AssistXP <= 0) return;
            if (!_damageTracker.TryGetValue(victimId, out var damageMap)) return;

            float totalDamage = 0f;
            foreach (var dmg in damageMap.Values)
                totalDamage += dmg;

            if (totalDamage <= 0f) return;

            foreach (var kvp in damageMap)
            {
                ulong assistPlayerId = kvp.Key;
                if (assistPlayerId == killerId) continue; // Killer already gets full XP

                float damagePercent = kvp.Value / totalDamage;
                if (damagePercent < _config.AssistDamageThreshold) continue;

                if (!_playerCache.TryGetValue(assistPlayerId, out PlayerData assistData)) continue;

                var assistPlayer = BasePlayer.FindByID(assistPlayerId);
                if (assistPlayer == null || !assistPlayer.IsConnected) continue;

                int assistXP = (int)(_config.AssistXP * _config.DifficultyMultiplier);
                if (assistData.Prestige > 0)
                    assistXP = (int)(assistXP * GetPrestigeXPMultiplier(assistData.Prestige));
                assistData.XP += assistXP;
                assistData.Dirty = true;

                bool leveledUp = CheckLevelUp(assistData);

                ShowXPGain(assistPlayer, assistData, assistXP, "AssistXP");
                if (leveledUp)
                    ShowLevelUp(assistPlayer, assistData);

                SavePlayer(assistData);
            }
        }

        private void HandleKillStreak(BasePlayer killer, PlayerData data)
        {
            var reward = _config.KillStreakRewards.FirstOrDefault(r => r.Streak == data.CurrentStreak);
            if (reward == null) return;

            // Show streak banner
            ShowStreakBanner(killer, reward.Title, data.CurrentStreak);

            // Broadcast to server
            if (reward.BroadcastToServer)
                PrintToChat($"{_config.ChatPrefix} {Lang("StreakAnnounce", null, killer.displayName, data.CurrentStreak, reward.Title)}");

            // Give streak reward item
            if (!string.IsNullOrEmpty(reward.RewardItemShortname) && reward.RewardItemAmount > 0)
            {
                var itemDef = ItemManager.FindItemDefinition(reward.RewardItemShortname);
                if (itemDef != null)
                {
                    var item = ItemManager.Create(itemDef, reward.RewardItemAmount);
                    if (item != null)
                    {
                        if (!killer.inventory.GiveItem(item))
                            item.DropAndTossUpwards(killer.transform.position);
                        killer.ChatMessage($"{_config.ChatPrefix} Streak reward: <color=#4CAF50>{reward.RewardItemAmount}x {itemDef.displayName.english}</color>!");
                    }
                }
            }
        }

        private void RefillActiveWeaponAmmo(BasePlayer player)
        {
            var activeItem = player.GetActiveItem();
            if (activeItem == null) return;

            var weapon = activeItem.GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;

            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
            weapon.SendNetworkUpdateImmediate();
        }

        private void GiveKillReward(BasePlayer player)
        {
            if (string.IsNullOrEmpty(_config.KillRewardItemShortname)) return;

            var itemDef = ItemManager.FindItemDefinition(_config.KillRewardItemShortname);
            if (itemDef == null)
            {
                PrintWarning($"Kill reward item '{_config.KillRewardItemShortname}' not found!");
                return;
            }

            int amount = UnityEngine.Random.Range(_config.KillRewardMinAmount, _config.KillRewardMaxAmount + 1);
            var item = ItemManager.Create(itemDef, amount);
            if (item == null) return;

            if (!player.inventory.GiveItem(item))
                item.DropAndTossUpwards(player.transform.position);

            player.ChatMessage($"{_config.ChatPrefix} You received <color=#4CAF50>{amount}x {itemDef.displayName.english}</color>!");
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
                    CreateXPBar(player, data);
                    SpawnProtection?.Call("ApplyProtection", player);
                    BountySystem?.Call("RefreshBountyUI", player);
                }
            });
        }

        private void OnNewSave(string filename)
        {
            if (_config.WipeOnNewSave)
            {
                Puts("New map save detected — wiping Gun Game data...");
                WipeDatabase();
                _firstBloodClaimed = false;
                _damageTracker.Clear();
                _lastKilledBy.Clear();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;

            // Clean up UI
            DestroyXPBar(player);
            CuiHelper.DestroyUi(player, CUI_XPGainPopup);
            CuiHelper.DestroyUi(player, CUI_LevelUpBanner);
            CuiHelper.DestroyUi(player, CUI_ScreenFlash);
            CuiHelper.DestroyUi(player, CUI_StatsBoard);
            CuiHelper.DestroyUi(player, CUI_StreakPopup);
            CuiHelper.DestroyUi(player, CUI_PrestigeBanner);

            // Clean up timers
            ulong id = player.userID;
            if (_xpPopupTimers.TryGetValue(id, out Timer t1)) { t1?.Destroy(); _xpPopupTimers.Remove(id); }
            if (_levelUpTimers.TryGetValue(id, out Timer t2)) { t2?.Destroy(); _levelUpTimers.Remove(id); }
            if (_flashTimers.TryGetValue(id, out Timer t3)) { t3?.Destroy(); _flashTimers.Remove(id); }
            if (_streakTimers.TryGetValue(id, out Timer t4)) { t4?.Destroy(); _streakTimers.Remove(id); }
            if (_prestigeTimers.TryGetValue(id, out Timer t5)) { t5?.Destroy(); _prestigeTimers.Remove(id); }
            _pendingXPDisplay.Remove(id);
            _prestigePending.Remove(id);

            // Reset current streak (they left)
            if (_playerCache.TryGetValue(player.userID, out PlayerData data))
            {
                data.CurrentStreak = 0;
                SavePlayer(data);
            }

            // Clean up tracking data
            _damageTracker.Remove(player.userID);
            _lastKilledBy.Remove(player.userID);
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

        #region CUI - Stats Board

        private void ShowStatsBoard(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_StatsBoard);

            var data = GetOrCreatePlayer(player);
            int nextXP = GetXPForNextLevel(data.Level);
            int currentLevelXP = GetXPForCurrentLevel(data.Level);
            bool isMax = nextXP == int.MaxValue;
            int xpIntoLevel = data.XP - currentLevelXP;
            int xpNeeded = isMax ? 1 : nextXP - currentLevelXP;
            float progress = isMax ? 1f : Mathf.Clamp01((float)xpIntoLevel / xpNeeded);
            int totalKills = data.Kills + data.AnimalKills + data.NPCKills;

            var elements = new CuiElementContainer();

            // Dim overlay (click to close)
            elements.Add(new CuiButton
            {
                Button = { Command = "gungame.closestats", Color = "0 0 0 0.7" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, "Overlay", CUI_StatsBoard);

            // Main panel — centered card
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.08 0.97", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.2 0.15", AnchorMax = "0.8 0.85" },
                CursorEnabled = true
            }, CUI_StatsBoard, CUI_StatsBoard + "_Main");

            // Top accent bar
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorAccent },
                RectTransform = { AnchorMin = "0 0.97", AnchorMax = "1 1" }
            }, CUI_StatsBoard + "_Main");

            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = "GUN GAME STATS", FontSize = 20, Align = TextAnchor.MiddleLeft, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.03 0.89", AnchorMax = "0.6 0.96" }
            }, CUI_StatsBoard + "_Main");

            // Close button (X)
            elements.Add(new CuiButton
            {
                Button = { Command = "gungame.closestats", Color = "0.9 0.25 0.2 0.85" },
                RectTransform = { AnchorMin = "0.94 0.91", AnchorMax = "0.98 0.96" },
                Text = { Text = "✕", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-bold.ttf" }
            }, CUI_StatsBoard + "_Main");

            // ─── LEFT COLUMN: Your Stats ───
            string leftPanel = CUI_StatsBoard + "_Left";

            // Section background
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.9" },
                RectTransform = { AnchorMin = "0.02 0.38", AnchorMax = "0.49 0.88" }
            }, CUI_StatsBoard + "_Main", leftPanel);

            // Section header bg
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 1" }
            }, leftPanel, leftPanel + "_Hdr");

            elements.Add(new CuiLabel
            {
                Text = { Text = $"  {data.DisplayName}", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = ColorAccent, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, leftPanel + "_Hdr");

            // Level display — big number
            elements.Add(new CuiLabel
            {
                Text = { Text = data.Level.ToString(), FontSize = 48, Align = TextAnchor.MiddleCenter, Color = isMax ? ColorGold : ColorAccent, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.02 0.55", AnchorMax = "0.25 0.87" }
            }, leftPanel);

            string levelSubtext = isMax ? "MAX LEVEL" : "LEVEL";
            if (data.Prestige > 0)
            {
                var statsTier = GetPrestigeTier(data.Prestige);
                string statsTierTitle = statsTier?.Title ?? $"Prestige {data.Prestige}";
                string statsTierColor = statsTier?.Color ?? "1.0 0.0 1.0 1.0";
                levelSubtext = isMax ? "MAX LEVEL" : "LEVEL";

                // Prestige tier badge
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"P{data.Prestige} {statsTierTitle}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = statsTierColor, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0.28 0.57", AnchorMax = "0.95 0.67" }
                }, leftPanel);
            }

            elements.Add(new CuiLabel
            {
                Text = { Text = levelSubtext, FontSize = 9, Align = TextAnchor.UpperCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.02 0.50", AnchorMax = "0.25 0.58" }
            }, leftPanel);

            // XP progress bar
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorBarEmpty },
                RectTransform = { AnchorMin = "0.28 0.68", AnchorMax = "0.95 0.75" }
            }, leftPanel, leftPanel + "_BarBg");

            string barFillMax = (0.28 + progress * (0.95 - 0.28)).ToString("F4");
            elements.Add(new CuiPanel
            {
                Image = { Color = isMax ? ColorBarFillMax : ColorBarFill },
                RectTransform = { AnchorMin = "0.28 0.68", AnchorMax = $"{barFillMax} 0.75" }
            }, leftPanel);

            // Bar glow
            if (progress > 0.02f)
            {
                elements.Add(new CuiPanel
                {
                    Image = { Color = "1.0 1.0 1.0 0.08" },
                    RectTransform = { AnchorMin = "0.28 0.72", AnchorMax = $"{barFillMax} 0.75" }
                }, leftPanel);
            }

            string xpLabel = isMax ? $"{data.XP} XP — MAX" : $"{xpIntoLevel} / {xpNeeded} XP";
            elements.Add(new CuiLabel
            {
                Text = { Text = xpLabel, FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.28 0.68", AnchorMax = "0.95 0.75" }
            }, leftPanel);

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Total XP: {data.XP}", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.28 0.59", AnchorMax = "0.95 0.67" }
            }, leftPanel);

            // Divider
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.25 0.25 0.25 0.6" },
                RectTransform = { AnchorMin = "0.04 0.48", AnchorMax = "0.96 0.485" }
            }, leftPanel);

            // Stats grid — 2 columns
            float statStartY = 0.36f;
            float statRowH = 0.11f;
            string[][] stats = new string[][]
            {
                new[] { "Player Kills", data.Kills.ToString(), "K/D Ratio", data.KDRatio.ToString() },
                new[] { "Deaths", data.Deaths.ToString(), "Headshots", data.Headshots.ToString() },
                new[] { "Animal Kills", data.AnimalKills.ToString(), "NPC Kills", data.NPCKills.ToString() },
                new[] { "Total Kills", totalKills.ToString(), "Best Streak", data.BestStreak.ToString() },
            };

            for (int r = 0; r < stats.Length; r++)
            {
                float yTop = statStartY + (stats.Length - 1 - r) * statRowH;
                float yBot = yTop - statRowH + 0.015f;

                // Left stat
                elements.Add(new CuiLabel
                {
                    Text = { Text = stats[r][0], FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.05 {yBot:F3}", AnchorMax = $"0.38 {yTop:F3}" }
                }, leftPanel);

                elements.Add(new CuiLabel
                {
                    Text = { Text = stats[r][1], FontSize = 11, Align = TextAnchor.MiddleRight, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = $"0.30 {yBot:F3}", AnchorMax = $"0.48 {yTop:F3}" }
                }, leftPanel);

                if (!string.IsNullOrEmpty(stats[r][2]))
                {
                    // Right stat
                    elements.Add(new CuiLabel
                    {
                        Text = { Text = stats[r][2], FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                        RectTransform = { AnchorMin = $"0.54 {yBot:F3}", AnchorMax = $"0.85 {yTop:F3}" }
                    }, leftPanel);

                    elements.Add(new CuiLabel
                    {
                        Text = { Text = stats[r][3], FontSize = 11, Align = TextAnchor.MiddleRight, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                        RectTransform = { AnchorMin = $"0.80 {yBot:F3}", AnchorMax = $"0.96 {yTop:F3}" }
                    }, leftPanel);
                }
            }

            // ─── RIGHT COLUMN: Leaderboard ───
            string rightPanel = CUI_StatsBoard + "_Right";

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.9" },
                RectTransform = { AnchorMin = "0.51 0.02", AnchorMax = "0.98 0.88" }
            }, CUI_StatsBoard + "_Main", rightPanel);

            // Section header
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" }
            }, rightPanel, rightPanel + "_Hdr");

            elements.Add(new CuiLabel
            {
                Text = { Text = "  TOP 10 LEADERBOARD", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = ColorGold, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, rightPanel + "_Hdr");

            // Column headers
            float headerY = 0.91f;
            elements.Add(new CuiLabel
            {
                Text = { Text = "#", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.02 {headerY - 0.035f}", AnchorMax = $"0.08 {headerY}" }
            }, rightPanel);
            elements.Add(new CuiLabel
            {
                Text = { Text = "PLAYER", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.10 {headerY - 0.035f}", AnchorMax = $"0.45 {headerY}" }
            }, rightPanel);
            elements.Add(new CuiLabel
            {
                Text = { Text = "LVL", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.46 {headerY - 0.035f}", AnchorMax = $"0.56 {headerY}" }
            }, rightPanel);
            elements.Add(new CuiLabel
            {
                Text = { Text = "XP", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.57 {headerY - 0.035f}", AnchorMax = $"0.70 {headerY}" }
            }, rightPanel);
            elements.Add(new CuiLabel
            {
                Text = { Text = "KILLS", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.71 {headerY - 0.035f}", AnchorMax = $"0.84 {headerY}" }
            }, rightPanel);
            elements.Add(new CuiLabel
            {
                Text = { Text = "K/D", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = $"0.85 {headerY - 0.035f}", AnchorMax = $"0.98 {headerY}" }
            }, rightPanel);

            // Header divider
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.3 0.3 0.3 0.5" },
                RectTransform = { AnchorMin = $"0.02 {headerY - 0.04f}", AnchorMax = $"0.98 {headerY - 0.037f}" }
            }, rightPanel);

            // Leaderboard rows
            var top = _playerCache.Values
                .OrderByDescending(p => p.Prestige)
                .ThenByDescending(p => p.Level)
                .ThenByDescending(p => p.XP)
                .ThenByDescending(p => p.Kills)
                .Take(10)
                .ToList();

            float rowH = 0.08f;
            float startY = headerY - 0.055f;

            for (int i = 0; i < 10; i++)
            {
                float rowTop = startY - i * rowH;
                float rowBot = rowTop - rowH + 0.01f;

                if (i >= top.Count)
                    break;

                var p = top[i];
                bool isMe = p.SteamId == player.userID;
                string rankColor = i == 0 ? ColorGold : i == 1 ? "0.8 0.8 0.8 1.0" : i == 2 ? "0.8 0.5 0.2 1.0" : ColorWhiteSoft;
                string nameColor = isMe ? ColorAccent : ColorWhite;
                int maxNameLen = p.Prestige > 0 ? 14 : 18;
                string nameText = p.DisplayName.Length > maxNameLen ? p.DisplayName.Substring(0, maxNameLen) + ".." : p.DisplayName;
                if (p.Prestige > 0)
                    nameText = $"P{p.Prestige} {nameText}";

                // Highlight row if it's the player
                if (isMe)
                {
                    elements.Add(new CuiPanel
                    {
                        Image = { Color = "1.0 0.4 0.0 0.08" },
                        RectTransform = { AnchorMin = $"0.01 {rowBot}", AnchorMax = $"0.99 {rowTop}" }
                    }, rightPanel);
                }

                // Rank
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"#{i + 1}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = rankColor, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = $"0.02 {rowBot}", AnchorMax = $"0.08 {rowTop}" }
                }, rightPanel);

                // Name
                elements.Add(new CuiLabel
                {
                    Text = { Text = nameText, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = nameColor, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.10 {rowBot}", AnchorMax = $"0.45 {rowTop}" }
                }, rightPanel);

                // Level
                elements.Add(new CuiLabel
                {
                    Text = { Text = p.Level.ToString(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = $"0.46 {rowBot}", AnchorMax = $"0.56 {rowTop}" }
                }, rightPanel);

                // XP
                elements.Add(new CuiLabel
                {
                    Text = { Text = p.XP.ToString(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.57 {rowBot}", AnchorMax = $"0.70 {rowTop}" }
                }, rightPanel);

                // Kills
                elements.Add(new CuiLabel
                {
                    Text = { Text = p.Kills.ToString(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.71 {rowBot}", AnchorMax = $"0.84 {rowTop}" }
                }, rightPanel);

                // K/D
                elements.Add(new CuiLabel
                {
                    Text = { Text = p.KDRatio.ToString(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.85 {rowBot}", AnchorMax = $"0.98 {rowTop}" }
                }, rightPanel);
            }

            if (top.Count == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = "No player data yet.", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.6" }
                }, rightPanel);
            }

            // ─── BOTTOM LEFT: Player rank info ───
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.9" },
                RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.49 0.36" }
            }, CUI_StatsBoard + "_Main", CUI_StatsBoard + "_Rank");

            // Find player rank
            var allSorted = _playerCache.Values
                .OrderByDescending(p => p.Prestige)
                .ThenByDescending(p => p.Level)
                .ThenByDescending(p => p.XP)
                .ThenByDescending(p => p.Kills)
                .ToList();
            int myRank = allSorted.FindIndex(p => p.SteamId == player.userID) + 1;
            string rankText = myRank > 0 ? $"#{myRank}" : "—";
            string rankOfText = myRank > 0 ? $"of {allSorted.Count} players" : "";

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, CUI_StatsBoard + "_Rank", CUI_StatsBoard + "_Rank_Hdr");

            elements.Add(new CuiLabel
            {
                Text = { Text = "  YOUR RANKING", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = ColorAccent, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, CUI_StatsBoard + "_Rank_Hdr");

            // Big rank number
            elements.Add(new CuiLabel
            {
                Text = { Text = rankText, FontSize = 42, Align = TextAnchor.MiddleCenter, Color = ColorAccent, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.4 0.82" }
            }, CUI_StatsBoard + "_Rank");

            elements.Add(new CuiLabel
            {
                Text = { Text = rankOfText, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.05 0.02", AnchorMax = "0.4 0.18" }
            }, CUI_StatsBoard + "_Rank");

            // Rank context stats
            string hsRate = data.Kills > 0 ? $"{Math.Round((double)data.Headshots / data.Kills * 100)}%" : "0%";
            elements.Add(new CuiLabel
            {
                Text = { Text = "Headshot Rate", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.45 0.58", AnchorMax = "0.78 0.72" }
            }, CUI_StatsBoard + "_Rank");
            elements.Add(new CuiLabel
            {
                Text = { Text = hsRate, FontSize = 13, Align = TextAnchor.MiddleRight, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.78 0.58", AnchorMax = "0.95 0.72" }
            }, CUI_StatsBoard + "_Rank");

            elements.Add(new CuiLabel
            {
                Text = { Text = "Total Kills", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.45 0.38", AnchorMax = "0.78 0.52" }
            }, CUI_StatsBoard + "_Rank");
            elements.Add(new CuiLabel
            {
                Text = { Text = totalKills.ToString(), FontSize = 13, Align = TextAnchor.MiddleRight, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.78 0.38", AnchorMax = "0.95 0.52" }
            }, CUI_StatsBoard + "_Rank");

            elements.Add(new CuiLabel
            {
                Text = { Text = "K/D Ratio", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.45 0.18", AnchorMax = "0.78 0.32" }
            }, CUI_StatsBoard + "_Rank");
            elements.Add(new CuiLabel
            {
                Text = { Text = data.KDRatio.ToString(), FontSize = 13, Align = TextAnchor.MiddleRight, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.78 0.18", AnchorMax = "0.95 0.32" }
            }, CUI_StatsBoard + "_Rank");

            // Hint at bottom
            elements.Add(new CuiLabel
            {
                Text = { Text = "Press ESC or click anywhere outside to close", FontSize = 9, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.3", Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.2 0.005", AnchorMax = "0.8 0.03" }
            }, CUI_StatsBoard + "_Main");

            CuiHelper.AddUi(player, elements);
        }

        [ConsoleCommand("gungame.closestats")]
        private void CmdCloseStats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, CUI_StatsBoard);
        }

        #endregion


        #region CUI - Streak Banner

        private void ShowStreakBanner(BasePlayer player, string title, int streak)
        {
            ulong id = player.userID;

            if (_streakTimers.TryGetValue(id, out Timer existing))
                existing?.Destroy();

            CuiHelper.DestroyUi(player, CUI_StreakPopup);

            var elements = new CuiElementContainer();

            string streakColor = streak >= 10 ? "1.0 0.2 0.2 1.0" : streak >= 7 ? "1.0 0.5 0.0 1.0" : "1.0 0.85 0.0 1.0";
            string bgColor = streak >= 10 ? "0.6 0.05 0.05 0.85" : "0.1 0.1 0.1 0.85";

            elements.Add(new CuiPanel
            {
                Image = { Color = bgColor, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.35 0.7", AnchorMax = "0.65 0.78" },
                CursorEnabled = false,
                FadeOut = 1.5f
            }, "Hud", CUI_StreakPopup);

            // Top accent
            elements.Add(new CuiPanel
            {
                Image = { Color = streakColor },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" },
                FadeOut = 1.5f
            }, CUI_StreakPopup);

            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = title, FontSize = 18, Align = TextAnchor.MiddleCenter, Color = streakColor, Font = "robotocondensed-bold.ttf", FadeIn = 0.15f },
                RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 0.9" },
                FadeOut = 1.5f
            }, CUI_StreakPopup);

            // Streak count
            elements.Add(new CuiLabel
            {
                Text = { Text = $"{streak} kills without dying", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.4" },
                FadeOut = 1.5f
            }, CUI_StreakPopup);

            CuiHelper.AddUi(player, elements);

            // Play streak sound
            Effect.server.Run("assets/bundled/prefabs/fx/gestures/drink_raise.prefab", player.transform.position);

            _streakTimers[id] = timer.Once(3.5f, () =>
            {
                if (player != null && player.IsConnected)
                    CuiHelper.DestroyUi(player, CUI_StreakPopup);
                _streakTimers.Remove(id);
            });
        }

        #endregion

        #region CUI - Prestige Banner

        private void ShowPrestigeBanner(BasePlayer player, PlayerData data)
        {
            ulong id = player.userID;

            if (_prestigeTimers.TryGetValue(id, out Timer existing))
                existing?.Destroy();

            CuiHelper.DestroyUi(player, CUI_PrestigeBanner);

            var elements = new CuiElementContainer();
            var tier = GetPrestigeTier(data.Prestige);
            string tierTitle = tier?.Title ?? $"Prestige {data.Prestige}";
            string tierColor = tier?.Color ?? "1.0 0.0 1.0 1.0";

            // Full-width banner near top of screen
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.02 0.08 0.95", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.2 0.75", AnchorMax = "0.8 0.92" },
                CursorEnabled = false,
                FadeOut = 2f
            }, "Hud", CUI_PrestigeBanner);

            // Top accent line
            elements.Add(new CuiPanel
            {
                Image = { Color = tierColor },
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                FadeOut = 2f
            }, CUI_PrestigeBanner);

            // Bottom accent line
            elements.Add(new CuiPanel
            {
                Image = { Color = tierColor },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.07" },
                FadeOut = 2f
            }, CUI_PrestigeBanner);

            // Prestige number (big, left side)
            elements.Add(new CuiLabel
            {
                Text = { Text = $"P{data.Prestige}", FontSize = 48, Align = TextAnchor.MiddleCenter, Color = tierColor, Font = "robotocondensed-bold.ttf", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0 0.07", AnchorMax = "0.22 0.93" },
                FadeOut = 2f
            }, CUI_PrestigeBanner);

            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = "PRESTIGE UP!", FontSize = 26, Align = TextAnchor.MiddleLeft, Color = tierColor, Font = "robotocondensed-bold.ttf", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0.24 0.55", AnchorMax = "0.95 0.93" },
                FadeOut = 2f
            }, CUI_PrestigeBanner);

            // Subtitle
            int bonusPercent = (int)(data.Prestige * _config.PrestigeXPBonusPercent * 100);
            elements.Add(new CuiLabel
            {
                Text = { Text = $"{tierTitle} — +{bonusPercent}% XP bonus", FontSize = 15, Align = TextAnchor.MiddleLeft, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf", FadeIn = 0.4f },
                RectTransform = { AnchorMin = "0.24 0.12", AnchorMax = "0.95 0.52" },
                FadeOut = 2f
            }, CUI_PrestigeBanner);

            CuiHelper.AddUi(player, elements);

            // Play prestige sound & gesture
            Effect.server.Run("assets/bundled/prefabs/fx/gestures/drink_raise.prefab", player.transform.position);
            Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", player.transform.position);
            player.SignalBroadcast(BaseEntity.Signal.Gesture, "victory");

            // Screen flash
            ShowScreenFlash(player);

            _prestigeTimers[id] = timer.Once(5f, () =>
            {
                if (player != null && player.IsConnected)
                    CuiHelper.DestroyUi(player, CUI_PrestigeBanner);
                _prestigeTimers.Remove(id);
            });
        }

        #endregion

        #region Bounty Integration

        private void UpdateBountyTarget()
        {
            if (BountySystem == null) return;

            var leader = _playerCache.Values
                .OrderByDescending(p => p.Kills)
                .FirstOrDefault();

            if (leader != null)
                BountySystem.Call("UpdateBounty", leader.SteamId, leader.DisplayName, leader.Kills);
        }

        #endregion

        #region Commands

        [ChatCommand("stats")]
        private void CmdStats(BasePlayer player, string command, string[] args)
        {
            ShowStatsBoard(player);
        }

        [ChatCommand("prestige")]
        private void CmdPrestige(BasePlayer player, string command, string[] args)
        {
            if (!_config.PrestigeEnabled)
            {
                Message(player, "PrestigeDisabled");
                return;
            }

            var data = GetOrCreatePlayer(player);

            if (data.Prestige >= _config.MaxPrestige)
            {
                Message(player, "PrestigeMaxed", _config.MaxPrestige);
                return;
            }

            if (data.Level < _config.MaxLevel)
            {
                Message(player, "PrestigeNotReady", _config.MaxLevel);
                return;
            }

            // Confirmation flow
            if (args.Length >= 1 && args[0].ToLower() == "confirm")
            {
                if (!_prestigePending.Contains(player.userID))
                {
                    // They typed /prestige confirm without first typing /prestige
                    var nextTier = GetPrestigeTier(data.Prestige + 1);
                    string nextTitle = nextTier?.Title ?? $"Prestige {data.Prestige + 1}";
                    Message(player, "PrestigeConfirm", data.Prestige + 1, nextTitle);
                    _prestigePending.Add(player.userID);
                    return;
                }

                _prestigePending.Remove(player.userID);

                // Perform prestige
                data.Prestige++;
                data.Level = 1;
                data.XP = 0;
                data.CurrentStreak = 0;
                data.Dirty = true;
                SavePlayer(data);

                // Give currency reward
                if (_config.PrestigeCurrencyReward > 0 && !string.IsNullOrEmpty(_config.KillRewardItemShortname))
                {
                    var itemDef = ItemManager.FindItemDefinition(_config.KillRewardItemShortname);
                    if (itemDef != null)
                    {
                        var item = ItemManager.Create(itemDef, _config.PrestigeCurrencyReward);
                        if (item != null)
                        {
                            if (!player.inventory.GiveItem(item))
                                item.DropAndTossUpwards(player.transform.position);
                        }
                    }
                }

                // Re-equip level 1 kit
                EquipKit(player, data.Level);

                // Show prestige banner
                ShowPrestigeBanner(player, data);

                // Update XP bar
                CreateXPBar(player, data);

                // Broadcast
                var tier = GetPrestigeTier(data.Prestige);
                string tierTitle = tier?.Title ?? $"Prestige {data.Prestige}";
                PrintToChat($"{_config.ChatPrefix} {Lang("PrestigeUp", null, player.displayName, data.Prestige, tierTitle)}");
                return;
            }

            // Show confirmation prompt
            var promptTier = GetPrestigeTier(data.Prestige + 1);
            string promptTitle = promptTier?.Title ?? $"Prestige {data.Prestige + 1}";
            Message(player, "PrestigeConfirm", data.Prestige + 1, promptTitle);
            _prestigePending.Add(player.userID);
        }

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
                        _firstBloodClaimed = false;
                        _damageTracker.Clear();
                        _lastKilledBy.Clear();

                        // Reset bounty
                        BountySystem?.Call("ClearBounty");

                        // Re-equip all online players with level 1 kit and refresh UI
                        foreach (var bp in BasePlayer.activePlayerList)
                        {
                            var data = GetOrCreatePlayer(bp);
                            EquipKit(bp, data.Level);
                            CreateXPBar(bp, data);
                        }

                        Message(player, "WipeComplete");
                    }
                    else
                    {
                        Message(player, "WipeConfirm");
                    }
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
            int currentLevelXP = GetXPForCurrentLevel(data.Level);
            int xpIntoLevel = data.XP - currentLevelXP;
            int xpNeeded = nextXP - currentLevelXP;
            string xpDisplay = nextXP == int.MaxValue ? $"{data.XP} (MAX)" : $"{xpIntoLevel}/{xpNeeded}";
            Message(player, "Stats", data.Level, xpDisplay, "", data.Kills, data.Deaths, data.KDRatio, data.Headshots, data.AnimalKills, data.NPCKills);
            if (data.Prestige > 0)
            {
                var tier = GetPrestigeTier(data.Prestige);
                string tierTitle = tier?.Title ?? $"Prestige {data.Prestige}";
                int bonusPct = (int)(data.Prestige * _config.PrestigeXPBonusPercent * 100);
                player.ChatMessage($"{_config.ChatPrefix} {Lang("PrestigeBonus", player, $"{data.Prestige} ({tierTitle})", bonusPct)}");
            }
        }

        private void ShowStatsOther(BasePlayer player, PlayerData target)
        {
            Message(player, "StatsOther", target.DisplayName, target.Level, target.XP, target.Kills, target.Deaths, target.KDRatio, target.Headshots, target.AnimalKills, target.NPCKills);
            if (target.Prestige > 0)
            {
                var tier = GetPrestigeTier(target.Prestige);
                string tierTitle = tier?.Title ?? $"Prestige {target.Prestige}";
                int bonusPct = (int)(target.Prestige * _config.PrestigeXPBonusPercent * 100);
                player.ChatMessage($"{_config.ChatPrefix} {Lang("PrestigeBonus", player, $"{target.Prestige} ({tierTitle})", bonusPct)}");
            }
        }

        private void ShowLeaderboard(BasePlayer player)
        {
            if (_playerCache.Count == 0)
            {
                Message(player, "TopEmpty");
                return;
            }

            var top = _playerCache.Values
                .OrderByDescending(p => p.Prestige)
                .ThenByDescending(p => p.Level)
                .ThenByDescending(p => p.XP)
                .ThenByDescending(p => p.Kills)
                .Take(_config.TopListSize)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(Lang("TopHeader", player));

            for (int i = 0; i < top.Count; i++)
            {
                var p = top[i];
                string displayName = p.Prestige > 0 ? $"[P{p.Prestige}] {p.DisplayName}" : p.DisplayName;
                sb.AppendLine(string.Format(Lang("TopEntry", player), i + 1, displayName, p.Level, p.XP, p.Kills, p.KDRatio));
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

            // Equip kit and update UI if online
            if (targetPlayer != null && targetPlayer.IsConnected)
            {
                EquipKit(targetPlayer, level);
                CreateXPBar(targetPlayer, targetData);
                ShowLevelUpBanner(targetPlayer, targetData);
                ShowScreenFlash(targetPlayer);
            }

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
