using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BountySystem", "rust-gg", "1.0.0")]
    [Description("Bounty on kill leader — map marker, CUI indicator, XP multiplier for killing the target")]
    public class BountySystem : RustPlugin
    {
        #region Fields

        private const string CUI_Bounty = "Bounty_Main";
        private ulong _currentTarget;
        private string _currentTargetName;
        private MapMarkerGenericRadius _marker;

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("BountyMultiplier")]
            public float BountyMultiplier { get; set; } = 2.0f;

            [JsonProperty("BountyMinKills")]
            public int BountyMinKills { get; set; } = 5;

            [JsonProperty("ChatPrefix")]
            public string ChatPrefix { get; set; } = "<color=#ff6600>[Bounty]</color>";
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
                if (_config == null) throw new System.Exception();
            }
            catch
            {
                PrintWarning("Invalid config, loading defaults...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Lifecycle

        private void Unload()
        {
            RemoveMarker();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, CUI_Bounty);
        }

        #endregion

        #region API

        // Called by GunGame: BountySystem.Call("UpdateBounty", steamId, playerName, kills)
        // Pass the current kill leader's info. The plugin decides if they qualify.
        private void UpdateBounty(ulong steamId, string playerName, int kills)
        {
            if (kills < _config.BountyMinKills)
            {
                if (_currentTarget != 0)
                    ClearBounty();
                return;
            }

            if (steamId == _currentTarget) return;

            RemoveMarker();
            _currentTarget = steamId;
            _currentTargetName = playerName;

            PrintToChat($"{_config.ChatPrefix} <color=#ffff00>{playerName}</color> is the kill leader! Kill them for <color=#00ff00>{_config.BountyMultiplier}x XP</color>!");

            var targetPlayer = BasePlayer.FindByID(_currentTarget);
            if (targetPlayer != null && targetPlayer.IsConnected)
                CreateMarker(targetPlayer);

            RefreshAllUI();
        }

        // Returns the bounty multiplier if the victim is the bounty target, otherwise 1.0
        private float GetBountyMultiplier(ulong victimSteamId)
        {
            if (victimSteamId == _currentTarget && _currentTarget != 0)
                return _config.BountyMultiplier;
            return 1f;
        }

        // Returns the current bounty target steam ID (0 if none)
        private ulong GetBountyTarget()
        {
            return _currentTarget;
        }

        private void ClearBounty()
        {
            _currentTarget = 0;
            _currentTargetName = null;
            RemoveMarker();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, CUI_Bounty);
        }

        // Called to refresh UI for a single player (e.g., on connect/respawn)
        private void RefreshBountyUI(BasePlayer player)
        {
            UpdateUI(player);
        }

        #endregion

        #region Hooks

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || _currentTarget == 0) return;
            timer.Once(2f, () =>
            {
                if (player != null && player.IsConnected)
                    UpdateUI(player);
            });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, CUI_Bounty);

            if (player.userID == _currentTarget)
                ClearBounty();
        }

        #endregion

        #region Map Marker

        private void CreateMarker(BasePlayer target)
        {
            RemoveMarker();

            _marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", target.transform.position) as MapMarkerGenericRadius;
            if (_marker == null) return;

            _marker.alpha = 0.6f;
            _marker.color1 = new Color(1f, 0.2f, 0.2f, 1f);
            _marker.color2 = new Color(1f, 0.5f, 0f, 1f);
            _marker.radius = 0.15f;
            _marker.SetParent(target);
            _marker.Spawn();
            _marker.SendUpdate();
        }

        private void RemoveMarker()
        {
            if (_marker != null && !_marker.IsDestroyed)
            {
                _marker.Kill();
                _marker = null;
            }
        }

        #endregion

        #region UI

        private void RefreshAllUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
                UpdateUI(player);
        }

        private void UpdateUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_Bounty);

            if (_currentTarget == 0 || string.IsNullOrEmpty(_currentTargetName)) return;

            bool isMe = player.userID == _currentTarget;
            string displayText = isMe
                ? "YOU ARE THE BOUNTY TARGET"
                : $"BOUNTY: {_currentTargetName} \u2014 {_config.BountyMultiplier}x XP";
            string textColor = isMe ? "1.0 0.3 0.3 1.0" : "1.0 0.85 0.0 1.0";
            string bgColor = isMe ? "0.8 0.1 0.1 0.12" : "1.0 0.4 0.0 0.12";

            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = bgColor },
                RectTransform = { AnchorMin = "0.35 0.91", AnchorMax = "0.65 0.945" },
                CursorEnabled = false
            }, "Hud", CUI_Bounty);

            elements.Add(new CuiLabel
            {
                Text = { Text = displayText, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = textColor, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, CUI_Bounty);

            CuiHelper.AddUi(player, elements);
        }

        #endregion
    }
}
