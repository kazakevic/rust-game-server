using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyMini", "rust-gg", "1.0.0")]
    [Description("Spawn a personal minicopter with /mymini")]
    public class MyMini : RustPlugin
    {
        private const string Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const float FuelAmount = 150f;
        private const float SpawnDistance = 5f;

        private Dictionary<ulong, ulong> _playerMinis = new Dictionary<ulong, ulong>();

        [ChatCommand("mymini")]
        private void CmdMyMini(BasePlayer player, string command, string[] args)
        {

            // Check if player already has an active mini
            if (_playerMinis.TryGetValue(player.userID, out ulong existingId))
            {
                var existing = BaseNetworkable.serverEntities.Find(new NetworkableId(existingId)) as Minicopter;
                if (existing != null && !existing.IsDestroyed)
                {
                    player.ChatMessage("You already have a minicopter. Use <color=#ff6600>/nomini</color> to remove it first.");
                    return;
                }
                _playerMinis.Remove(player.userID);
            }

            // Find spawn position in front of the player
            Vector3 spawnPos = player.transform.position + player.eyes.BodyForward() * SpawnDistance;
            spawnPos.y = player.transform.position.y + 1f;

            // Spawn the minicopter
            var mini = GameManager.server.CreateEntity(Prefab, spawnPos, Quaternion.LookRotation(player.eyes.BodyForward())) as Minicopter;
            if (mini == null)
            {
                player.ChatMessage("Failed to spawn minicopter.");
                return;
            }

            mini.OwnerID = player.userID;
            mini.Spawn();

            // Add fuel
            var fuelSystem = mini.GetFuelSystem();
            if (fuelSystem != null)
            {
                var fuelContainer = fuelSystem.GetFuelContainer();
                if (fuelContainer?.inventory != null)
                {
                    var fuelItem = ItemManager.CreateByName("lowgradefuel", (int)FuelAmount);
                    if (fuelItem != null)
                        fuelItem.MoveToContainer(fuelContainer.inventory);
                }
            }

            _playerMinis[player.userID] = mini.net.ID.Value;
            player.ChatMessage($"Minicopter spawned with <color=#ff6600>{(int)FuelAmount}</color> fuel!");
        }

        [ChatCommand("nomini")]
        private void CmdNoMini(BasePlayer player, string command, string[] args)
        {
            if (!_playerMinis.TryGetValue(player.userID, out ulong miniId))
            {
                player.ChatMessage("You don't have an active minicopter.");
                return;
            }

            var mini = BaseNetworkable.serverEntities.Find(new NetworkableId(miniId)) as Minicopter;
            if (mini == null || mini.IsDestroyed)
            {
                _playerMinis.Remove(player.userID);
                player.ChatMessage("Your minicopter no longer exists.");
                return;
            }

            mini.Kill();
            _playerMinis.Remove(player.userID);
            player.ChatMessage("Minicopter removed.");
        }

        private void OnEntityKill(Minicopter mini)
        {
            if (mini == null) return;

            // Clean up tracking when a mini is destroyed
            if (_playerMinis.ContainsKey(mini.OwnerID) && _playerMinis[mini.OwnerID] == mini.net.ID.Value)
                _playerMinis.Remove(mini.OwnerID);
        }

        private void Unload()
        {
            _playerMinis.Clear();
        }
    }
}
