using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("QuickSmelt", "rust-gg", "1.0.0")]
    [Description("Increases smelting speed and charcoal output in furnaces and campfires")]
    public class QuickSmelt : RustPlugin
    {
        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Smelt speed multiplier (1 = default)")]
            public float SpeedMultiplier { get; set; } = 5f;

            [JsonProperty("Charcoal per wood (default 1)")]
            public int CharcoalPerWood { get; set; } = 1;

            [JsonProperty("Output stack multiplier (1 = default)")]
            public float OutputMultiplier { get; set; } = 1f;

            [JsonProperty("Affected oven prefabs (empty = all)")]
            public List<string> AffectedPrefabs { get; set; } = new List<string>();
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

        private void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!oven.IsOn()) return;
            if (!IsAffected(oven)) return;

            AdjustOven(oven);
        }

        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!IsAffected(oven)) return;

            // Speed: cook extra items per tick
            int extra = (int)(_config.SpeedMultiplier - 1);
            for (int i = 0; i < extra; i++)
                oven.Cook();

            // Charcoal output
            if (fuel?.info?.shortname == "wood" && _config.CharcoalPerWood > 1)
            {
                Item charcoal = ItemManager.CreateByName("charcoal", _config.CharcoalPerWood - 1);
                if (charcoal != null && !charcoal.MoveToContainer(oven.inventory))
                    charcoal.Drop(oven.transform.position, UnityEngine.Vector3.zero);
            }
        }

        private void OnItemSmelted(BaseOven oven, Item item, Item outputItem)
        {
            if (!IsAffected(oven)) return;
            if (_config.OutputMultiplier <= 1f) return;

            int bonus = (int)(outputItem.amount * (_config.OutputMultiplier - 1f));
            if (bonus > 0)
                outputItem.amount += bonus;
        }

        #endregion

        #region Helpers

        private bool IsAffected(BaseOven oven)
        {
            if (_config.AffectedPrefabs == null || _config.AffectedPrefabs.Count == 0)
                return true;

            string prefab = oven.ShortPrefabName;
            return _config.AffectedPrefabs.Contains(prefab);
        }

        private void AdjustOven(BaseOven oven)
        {
            oven.cookingTemperature = oven.cookingTemperature * _config.SpeedMultiplier;
        }

        #endregion
    }
}
