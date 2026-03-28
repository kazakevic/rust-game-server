using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SpawnProtection", "rust-gg", "1.0.0")]
    [Description("Spawn protection — temporary invulnerability after respawn, removed on shooting")]
    public class SpawnProtection : RustPlugin
    {
        #region Fields

        private const string CUI_Badge = "SpawnProt_Badge";
        private Dictionary<ulong, Timer> _timers = new Dictionary<ulong, Timer>();
        private HashSet<ulong> _protected = new HashSet<ulong>();

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("ProtectionSeconds")]
            public float ProtectionSeconds { get; set; } = 3.0f;
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

        #region API

        // Called by GunGame: SpawnProtection.Call("ApplyProtection", player)
        private void ApplyProtection(BasePlayer player)
        {
            if (player == null || _config.ProtectionSeconds <= 0) return;

            ulong id = player.userID;
            RemoveProtectionInternal(player);

            _protected.Add(id);
            ShowBadge(player);

            _timers[id] = timer.Once(_config.ProtectionSeconds, () =>
            {
                RemoveProtectionInternal(player);
            });
        }

        // Called by GunGame: SpawnProtection.Call("RemoveProtection", player)
        private void RemoveProtection(BasePlayer player)
        {
            RemoveProtectionInternal(player);
        }

        // Returns true if player is currently protected
        private bool IsProtected(ulong steamId)
        {
            return _protected.Contains(steamId);
        }

        #endregion

        #region Hooks

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer victim && _protected.Contains(victim.userID))
            {
                info.damageTypes.ScaleAll(0f);
                return true;
            }
            return null;
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player != null && _protected.Contains(player.userID))
                RemoveProtectionInternal(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player != null)
                RemoveProtectionInternal(player);
        }

        #endregion

        #region Lifecycle

        private void Unload()
        {
            foreach (var t in _timers.Values) t?.Destroy();
            _timers.Clear();
            _protected.Clear();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, CUI_Badge);
        }

        #endregion

        #region Internal

        private void RemoveProtectionInternal(BasePlayer player)
        {
            if (player == null) return;
            ulong id = player.userID;

            _protected.Remove(id);

            if (_timers.TryGetValue(id, out Timer t))
            {
                t?.Destroy();
                _timers.Remove(id);
            }

            if (player.IsConnected)
                CuiHelper.DestroyUi(player, CUI_Badge);
        }

        private void ShowBadge(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_Badge);

            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.0 0.6 1.0 0.15" },
                RectTransform = { AnchorMin = "0.4 0.85", AnchorMax = "0.6 0.89" },
                CursorEnabled = false,
                FadeOut = 0.5f
            }, "Hud", CUI_Badge);

            elements.Add(new CuiLabel
            {
                Text = { Text = "SPAWN PROTECTED", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.3 0.8 1.0 1.0", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                FadeOut = 0.5f
            }, CUI_Badge);

            CuiHelper.AddUi(player, elements);
        }

        #endregion
    }
}
