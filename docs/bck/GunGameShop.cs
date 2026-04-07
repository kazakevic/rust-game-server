using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GunGameShop", "rust-gg", "1.0.0")]
    [Description("In-game shop for Gun Game — buy weapon attachments and items with kill currency")]
    public class GunGameShop : RustPlugin
    {
        #region Fields

        private const string CUI_Shop = "GGShop";
        private Dictionary<ulong, int> _shopCategory = new Dictionary<ulong, int>();

        private const string ColorAccent = "1.0 0.4 0.0 1.0";
        private const string ColorWhite = "1.0 1.0 1.0 1.0";
        private const string ColorWhiteSoft = "1.0 1.0 1.0 0.7";
        private const string ColorGreen = "0.0 1.0 0.3 1.0";

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("CurrencyItemShortname")]
            public string CurrencyItemShortname { get; set; } = "blood";

            [JsonProperty("ChatPrefix")]
            public string ChatPrefix { get; set; } = "<color=#ff6600>[Shop]</color>";

            [JsonProperty("ShopCategories")]
            public List<ShopCategory> ShopCategories { get; set; } = new List<ShopCategory>
            {
                new ShopCategory
                {
                    Name = "Weapon Attachments",
                    Items = new List<ShopItem>
                    {
                        new ShopItem { Shortname = "weapon.mod.lasersight", DisplayName = "Laser Sight", Price = 3 },
                        new ShopItem { Shortname = "weapon.mod.flashlight", DisplayName = "Flashlight", Price = 2 },
                        new ShopItem { Shortname = "weapon.mod.holosight", DisplayName = "Holosight", Price = 5 },
                        new ShopItem { Shortname = "weapon.mod.simplesight", DisplayName = "Simple Sight", Price = 3 },
                        new ShopItem { Shortname = "weapon.mod.small.scope", DisplayName = "Small Scope", Price = 8 },
                        new ShopItem { Shortname = "weapon.mod.8x.scope", DisplayName = "8x Scope", Price = 15 },
                        new ShopItem { Shortname = "weapon.mod.silencer", DisplayName = "Silencer", Price = 10 },
                        new ShopItem { Shortname = "weapon.mod.muzzleboost", DisplayName = "Muzzle Boost", Price = 5 },
                        new ShopItem { Shortname = "weapon.mod.muzzlebrake", DisplayName = "Muzzle Brake", Price = 5 },
                        new ShopItem { Shortname = "weapon.mod.extendedmags", DisplayName = "Extended Mag", Price = 12 },
                    }
                }
            };
        }

        private class ShopCategory
        {
            [JsonProperty("Name")]
            public string Name { get; set; }

            [JsonProperty("Items")]
            public List<ShopItem> Items { get; set; } = new List<ShopItem>();
        }

        private class ShopItem
        {
            [JsonProperty("Shortname")]
            public string Shortname { get; set; }

            [JsonProperty("DisplayName")]
            public string DisplayName { get; set; }

            [JsonProperty("Price")]
            public int Price { get; set; }
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

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ShopEmpty"] = "The shop has no items configured.",
                ["ShopError"] = "<color=#ff0000>Something went wrong with your purchase.</color>",
                ["ShopCantAfford"] = "You can't afford <color=#ffff00>{0}</color>!",
                ["ShopPurchased"] = "You purchased <color=#4CAF50>{0}</color> for <color=#ffff00>{1}</color> currency!",
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

        #region API

        // Called by GunGame or any other plugin: GunGameShop.Call("OpenShop", player, 0)
        private void OpenShop(BasePlayer player, int categoryIndex = 0)
        {
            ShowShop(player, categoryIndex);
        }

        private void CloseShopUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_Shop);
            _shopCategory.Remove(player.userID);
        }

        #endregion

        #region Lifecycle

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, CUI_Shop);
            _shopCategory.Clear();
        }

        #endregion

        #region Commands

        [ChatCommand("shop")]
        private void CmdShop(BasePlayer player, string command, string[] args)
        {
            int catIndex = 0;
            if (args.Length >= 1 && int.TryParse(args[0], out int idx))
                catIndex = idx;
            ShowShop(player, catIndex);
        }

        #endregion

        #region Shop UI

        private void ShowShop(BasePlayer player, int categoryIndex = 0)
        {
            if (_config.ShopCategories == null || _config.ShopCategories.Count == 0)
            {
                Message(player, "ShopEmpty");
                return;
            }

            categoryIndex = Mathf.Clamp(categoryIndex, 0, _config.ShopCategories.Count - 1);
            _shopCategory[player.userID] = categoryIndex;

            CuiHelper.DestroyUi(player, CUI_Shop);

            var elements = new CuiElementContainer();
            var category = _config.ShopCategories[categoryIndex];

            int balance = GetCurrencyBalance(player);
            string currencyName = _config.CurrencyItemShortname;
            var currencyDef = ItemManager.FindItemDefinition(currencyName);
            string currencyDisplay = currencyDef != null ? currencyDef.displayName.english : currencyName;

            // Full-screen dim background (click to close)
            elements.Add(new CuiButton
            {
                Button = { Command = "ggshop.close", Color = "0 0 0 0.7" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, "Overlay", CUI_Shop);

            // Main panel
            string mainPanel = CUI_Shop + "_Main";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.08 0.97", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.15 0.12", AnchorMax = "0.85 0.88" },
                CursorEnabled = true
            }, CUI_Shop, mainPanel);

            // Title bar
            elements.Add(new CuiPanel
            {
                Image = { Color = ColorAccent },
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" }
            }, mainPanel, mainPanel + "_Title");

            elements.Add(new CuiLabel
            {
                Text = { Text = "SHOP", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.05 0.05 0.05 1.0", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.3 1" }
            }, mainPanel + "_Title");

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Balance: {balance} {currencyDisplay}", FontSize = 13, Align = TextAnchor.MiddleRight, Color = "0.05 0.05 0.05 1.0", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.92 1" }
            }, mainPanel + "_Title");

            // Close button
            elements.Add(new CuiButton
            {
                Button = { Command = "ggshop.close", Color = "0.9 0.25 0.2 0.85" },
                RectTransform = { AnchorMin = "0.955 0.935", AnchorMax = "0.99 0.99" },
                Text = { Text = "✕", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-bold.ttf" }
            }, mainPanel);

            // Categories sidebar
            string sidebar = mainPanel + "_Sidebar";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.06 0.06 0.06 0.95" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.22 0.93" }
            }, mainPanel, sidebar);

            elements.Add(new CuiLabel
            {
                Text = { Text = "CATEGORIES", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" }
            }, sidebar);

            for (int i = 0; i < _config.ShopCategories.Count; i++)
            {
                float top = 0.92f - i * 0.08f;
                float bottom = top - 0.07f;
                bool isActive = i == categoryIndex;
                string btnColor = isActive ? "1.0 0.4 0.0 0.3" : "0.15 0.15 0.15 0.6";
                string textColor = isActive ? ColorAccent : ColorWhiteSoft;

                elements.Add(new CuiButton
                {
                    Button = { Command = $"ggshop.category {i}", Color = btnColor },
                    RectTransform = { AnchorMin = $"0.05 {bottom:F3}", AnchorMax = $"0.95 {top:F3}" },
                    Text = { Text = _config.ShopCategories[i].Name, FontSize = 11, Align = TextAnchor.MiddleCenter, Color = textColor, Font = "robotocondensed-bold.ttf" }
                }, sidebar);
            }

            // Items grid
            string grid = mainPanel + "_Grid";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.23 0", AnchorMax = "1 0.93" }
            }, mainPanel, grid);

            elements.Add(new CuiLabel
            {
                Text = { Text = category.Name.ToUpper(), FontSize = 14, Align = TextAnchor.MiddleLeft, Color = ColorAccent, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.98 1" }
            }, grid);

            elements.Add(new CuiPanel
            {
                Image = { Color = "1.0 0.4 0.0 0.3" },
                RectTransform = { AnchorMin = "0.02 0.915", AnchorMax = "0.98 0.918" }
            }, grid);

            int cols = 3;
            float cardW = 0.3f;
            float cardH = 0.27f;
            float gapX = 0.025f;
            float gapY = 0.02f;
            float startX = 0.02f;
            float startY = 0.88f;

            for (int i = 0; i < category.Items.Count; i++)
            {
                var item = category.Items[i];
                int col = i % cols;
                int row = i / cols;

                float x1 = startX + col * (cardW + gapX);
                float y2 = startY - row * (cardH + gapY);
                float x2 = x1 + cardW;
                float y1 = y2 - cardH;

                if (y1 < 0.01f) break;

                string cardName = grid + $"_Item{i}";
                bool canAfford = balance >= item.Price;

                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.12 0.12 0.12 0.9" },
                    RectTransform = { AnchorMin = $"{x1:F3} {y1:F3}", AnchorMax = $"{x2:F3} {y2:F3}" }
                }, grid, cardName);

                elements.Add(new CuiElement
                {
                    Parent = cardName,
                    Components =
                    {
                        new CuiRawImageComponent { Url = $"https://rustlabs.com/img/items180/{item.Shortname}.png", Color = "1 1 1 0.9" },
                        new CuiRectTransformComponent { AnchorMin = "0.25 0.48", AnchorMax = "0.75 0.95" }
                    }
                });

                elements.Add(new CuiLabel
                {
                    Text = { Text = item.DisplayName, FontSize = 11, Align = TextAnchor.MiddleCenter, Color = ColorWhite, Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0.05 0.32", AnchorMax = "0.95 0.48" }
                }, cardName);

                string priceColor = canAfford ? ColorGreen : "1.0 0.3 0.3 1.0";
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"{item.Price} {currencyDisplay}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = priceColor, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = "0.05 0.2", AnchorMax = "0.95 0.34" }
                }, cardName);

                string buyColor = canAfford ? "0.2 0.7 0.3 0.85" : "0.3 0.3 0.3 0.5";
                string buyTextColor = canAfford ? ColorWhite : "1.0 1.0 1.0 0.3";
                string buyCmd = canAfford ? $"ggshop.buy {categoryIndex} {i}" : "";

                elements.Add(new CuiButton
                {
                    Button = { Command = buyCmd, Color = buyColor },
                    RectTransform = { AnchorMin = "0.15 0.04", AnchorMax = "0.85 0.19" },
                    Text = { Text = canAfford ? "BUY" : "CAN'T AFFORD", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = buyTextColor, Font = "robotocondensed-bold.ttf" }
                }, cardName);
            }

            if (category.Items.Count == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = "No items in this category.", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = ColorWhiteSoft, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = "0 0.3", AnchorMax = "1 0.7" }
                }, grid);
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Currency

        private int GetCurrencyBalance(BasePlayer player)
        {
            if (string.IsNullOrEmpty(_config.CurrencyItemShortname)) return 0;
            int total = 0;
            foreach (var item in player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).Concat(player.inventory.containerWear.itemList))
            {
                if (item.info.shortname == _config.CurrencyItemShortname)
                    total += item.amount;
            }
            return total;
        }

        private bool TakeCurrency(BasePlayer player, int amount)
        {
            if (GetCurrencyBalance(player) < amount) return false;

            int remaining = amount;
            var items = player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).Concat(player.inventory.containerWear.itemList)
                .Where(i => i.info.shortname == _config.CurrencyItemShortname)
                .ToList();

            foreach (var item in items)
            {
                if (remaining <= 0) break;

                if (item.amount <= remaining)
                {
                    remaining -= item.amount;
                    item.RemoveFromContainer();
                    item.Remove();
                }
                else
                {
                    item.amount -= remaining;
                    item.MarkDirty();
                    remaining = 0;
                }
            }

            return remaining <= 0;
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("ggshop.close")]
        private void CmdCloseShop(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, CUI_Shop);
            _shopCategory.Remove(player.userID);
        }

        [ConsoleCommand("ggshop.category")]
        private void CmdShopCategory(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            int catIndex = arg.GetInt(0, 0);
            ShowShop(player, catIndex);
        }

        [ConsoleCommand("ggshop.buy")]
        private void CmdShopBuy(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            int catIndex = arg.GetInt(0, -1);
            int itemIndex = arg.GetInt(1, -1);

            if (catIndex < 0 || catIndex >= _config.ShopCategories.Count)
            {
                Message(player, "ShopError");
                return;
            }

            var category = _config.ShopCategories[catIndex];
            if (itemIndex < 0 || itemIndex >= category.Items.Count)
            {
                Message(player, "ShopError");
                return;
            }

            var shopItem = category.Items[itemIndex];

            var itemDef = ItemManager.FindItemDefinition(shopItem.Shortname);
            if (itemDef == null)
            {
                Message(player, "ShopError");
                PrintWarning($"Shop item '{shopItem.Shortname}' not found in game!");
                return;
            }

            if (GetCurrencyBalance(player) < shopItem.Price)
            {
                Message(player, "ShopCantAfford", shopItem.DisplayName);
                ShowShop(player, catIndex);
                return;
            }

            if (!TakeCurrency(player, shopItem.Price))
            {
                Message(player, "ShopError");
                return;
            }

            var newItem = ItemManager.Create(itemDef, 1);
            if (newItem == null)
            {
                Message(player, "ShopError");
                return;
            }

            if (!player.inventory.GiveItem(newItem))
                newItem.DropAndTossUpwards(player.transform.position);

            Message(player, "ShopPurchased", shopItem.DisplayName, shopItem.Price);
            Effect.server.Run("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player.transform.position);

            ShowShop(player, catIndex);
        }

        #endregion
    }
}
