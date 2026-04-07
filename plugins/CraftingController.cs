using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CraftingController", "rust-gg", "1.0.0")]
    [Description("Block or allow crafting of specific items by shortname")]
    public class CraftingController : RustPlugin
    {
        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Blocked item shortnames")]
            public HashSet<string> BlockedItems { get; set; } = new HashSet<string>();

            [JsonProperty("Block message")]
            public string BlockMessage { get; set; } = "You cannot craft <color=#ff4444>{0}</color> on this server.";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Hooks

        private object OnItemCraft(ItemCraftTask task, BasePlayer player, Item fromTempBlueprint)
        {
            if (task?.blueprint == null || player == null) return null;

            string shortname = task.blueprint.targetItem?.shortname;
            if (shortname == null) return null;

            if (_config.BlockedItems.Contains(shortname))
            {
                player.ChatMessage(string.Format(_config.BlockMessage, shortname));
                return true; // cancel crafting
            }

            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("craft.block")]
        private void CmdBlock(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("No permission."); return; }
            if (args.Length < 1) { player.ChatMessage("Usage: /craft.block <shortname>"); return; }

            string shortname = args[0].ToLower();
            if (_config.BlockedItems.Add(shortname))
            {
                SaveConfig();
                player.ChatMessage($"<color=#ff4444>{shortname}</color> is now blocked from crafting.");
            }
            else
            {
                player.ChatMessage($"{shortname} is already blocked.");
            }
        }

        [ChatCommand("craft.unblock")]
        private void CmdUnblock(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("No permission."); return; }
            if (args.Length < 1) { player.ChatMessage("Usage: /craft.unblock <shortname>"); return; }

            string shortname = args[0].ToLower();
            if (_config.BlockedItems.Remove(shortname))
            {
                SaveConfig();
                player.ChatMessage($"<color=#44ff44>{shortname}</color> is now unblocked.");
            }
            else
            {
                player.ChatMessage($"{shortname} was not blocked.");
            }
        }

        [ChatCommand("craft.list")]
        private void CmdList(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("No permission."); return; }

            if (_config.BlockedItems.Count == 0)
            {
                player.ChatMessage("No items are currently blocked.");
                return;
            }

            player.ChatMessage("Blocked items:\n" + string.Join("\n", _config.BlockedItems));
        }

        #endregion
    }
}
