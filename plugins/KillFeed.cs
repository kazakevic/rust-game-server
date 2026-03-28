using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("KillFeed", "rust-gg", "1.0.0")]
    [Description("CS2-style kill feed HUD — shows recent kills with headshot and distance indicators")]
    public class KillFeed : RustPlugin
    {
        #region Fields

        private const string CUI_KillFeed = "KillFeed_Main";
        private List<KillFeedEntry> _entries = new List<KillFeedEntry>();
        private Timer _cleanupTimer;

        private class KillFeedEntry
        {
            public string KillerName { get; set; }
            public string VictimName { get; set; }
            public string WeaponName { get; set; }
            public bool IsHeadshot { get; set; }
            public float Distance { get; set; }
            public float Timestamp { get; set; }
        }

        #endregion

        #region Lifecycle

        private void OnServerInitialized()
        {
            _cleanupTimer = timer.Every(2f, CleanupEntries);
        }

        private void Unload()
        {
            _cleanupTimer?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, CUI_KillFeed);
        }

        #endregion

        #region API

        // Called by other plugins: KillFeed.Call("AddEntry", killerName, victimName, weaponName, isHeadshot, distance)
        private void AddEntry(string killerName, string victimName, string weaponName, bool isHeadshot, float distance)
        {
            _entries.Add(new KillFeedEntry
            {
                KillerName = killerName,
                VictimName = victimName,
                WeaponName = weaponName,
                IsHeadshot = isHeadshot,
                Distance = distance,
                Timestamp = Time.realtimeSinceStartup
            });

            while (_entries.Count > 5)
                _entries.RemoveAt(0);

            RefreshAll();
        }

        #endregion

        #region Core

        private void CleanupEntries()
        {
            float now = Time.realtimeSinceStartup;
            int removed = _entries.RemoveAll(e => now - e.Timestamp > 8f);
            if (removed > 0)
                RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
                ShowFeed(player);
        }

        private void ShowFeed(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_KillFeed);

            if (_entries.Count == 0) return;

            float now = Time.realtimeSinceStartup;
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.68 0.78", AnchorMax = "0.995 0.93" },
                CursorEnabled = false
            }, "Hud", CUI_KillFeed);

            float rowH = 1f / 5f;
            int displayIndex = 0;

            for (int i = _entries.Count - 1; i >= 0 && displayIndex < 5; i--)
            {
                var entry = _entries[i];
                float age = now - entry.Timestamp;
                float alpha = age > 6f ? Math.Max(0f, 1f - (age - 6f) / 2f) : 1f;
                if (alpha <= 0f) continue;

                float yTop = 1f - displayIndex * rowH;
                float yBot = yTop - rowH + 0.01f;

                string killerColor = $"1.0 0.85 0.0 {alpha:F2}";
                string arrowColor = entry.IsHeadshot ? $"1.0 0.3 0.3 {alpha:F2}" : $"0.7 0.7 0.7 {alpha:F2}";
                string victimColor = $"1.0 1.0 1.0 {alpha:F2}";
                string arrow = entry.IsHeadshot ? "HS" : ">";

                string killerTrunc = entry.KillerName.Length > 14 ? entry.KillerName.Substring(0, 14) : entry.KillerName;
                string victimTrunc = entry.VictimName.Length > 14 ? entry.VictimName.Substring(0, 14) : entry.VictimName;
                string distText = entry.Distance > 10f ? $" ({entry.Distance:F0}m)" : "";

                elements.Add(new CuiPanel
                {
                    Image = { Color = $"0.05 0.05 0.05 {(0.6f * alpha):F2}" },
                    RectTransform = { AnchorMin = $"0 {yBot:F3}", AnchorMax = $"1 {yTop:F3}" }
                }, CUI_KillFeed);

                elements.Add(new CuiLabel
                {
                    Text = { Text = killerTrunc, FontSize = 10, Align = TextAnchor.MiddleRight, Color = killerColor, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = $"0 {yBot:F3}", AnchorMax = $"0.38 {yTop:F3}" }
                }, CUI_KillFeed);

                elements.Add(new CuiLabel
                {
                    Text = { Text = $" [{arrow}] ", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = arrowColor, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = $"0.39 {yBot:F3}", AnchorMax = $"0.56 {yTop:F3}" }
                }, CUI_KillFeed);

                elements.Add(new CuiLabel
                {
                    Text = { Text = $"{victimTrunc}{distText}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = victimColor, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.57 {yBot:F3}", AnchorMax = $"1 {yTop:F3}" }
                }, CUI_KillFeed);

                displayIndex++;
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion
    }
}
