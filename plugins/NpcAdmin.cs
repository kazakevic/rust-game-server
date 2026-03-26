using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NpcAdmin", "rust-gg", "1.1.0")]
    [Description("RCON bridge for HumanNPC — spawn and manage NPCs via console commands")]
    public class NpcAdmin : RustPlugin
    {
        [PluginReference]
        private Plugin HumanNPC;

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

            var result = HumanNPC.Call("CreateNPC", position, rotation, npcName, (ulong)0, true);
            if (result == null)
            {
                arg.ReplyWith("ERROR: Failed to spawn NPC — CreateNPC returned null");
                return;
            }

            // CreateNPC returns a HumanPlayer component; get the BasePlayer's userID
            var npcPlayer = (result as MonoBehaviour)?.GetComponent<BasePlayer>();
            if (npcPlayer == null)
            {
                // Fallback: try CreateNPCHook which returns BasePlayer directly
                var hookResult = HumanNPC.Call("CreateNPCHook", position, rotation, npcName, (ulong)0, true);
                if (hookResult == null)
                {
                    arg.ReplyWith("ERROR: Failed to spawn NPC");
                    return;
                }
                npcPlayer = hookResult as BasePlayer;
                if (npcPlayer == null)
                {
                    arg.ReplyWith("ERROR: Failed to get spawned NPC reference");
                    return;
                }
            }

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
                count++;
            }

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

            // Find the NPC's HumanPlayer component to modify its info
            var humanPlayer = HumanNPC.Call("FindHumanPlayerByID", npcId);
            if (humanPlayer == null)
            {
                arg.ReplyWith("ERROR: NPC not found");
                return;
            }

            string option = arg.Args[1].ToLower();
            string value = string.Join(" ", arg.Args.Skip(2));

            // Access the NPC's BasePlayer to modify properties directly
            var npcPlayer = (humanPlayer as MonoBehaviour)?.GetComponent<BasePlayer>();
            if (npcPlayer == null)
            {
                arg.ReplyWith("ERROR: Could not access NPC player");
                return;
            }

            switch (option)
            {
                case "health":
                    float hp;
                    if (float.TryParse(value, out hp))
                    {
                        npcPlayer.health = hp;
                        npcPlayer.SendNetworkUpdate();
                    }
                    break;
                case "kit":
                case "hostile":
                case "invulnerable":
                case "invulnerability":
                case "lootable":
                case "respawn":
                case "damageamount":
                case "speed":
                case "radius":
                case "collisionradius":
                case "defend":
                case "evade":
                case "follow":
                    // These require access to HumanNPCInfo internals
                    // Try reflection to set the property on the info object
                    try
                    {
                        var infoField = humanPlayer.GetType().GetField("info");
                        if (infoField != null)
                        {
                            var info = infoField.GetValue(humanPlayer);
                            if (info != null)
                            {
                                SetInfoProperty(info, option, value);
                                // Refresh the NPC to apply changes
                                HumanNPC.Call("UpdateNPC", npcPlayer, false);
                            }
                        }
                    }
                    catch
                    {
                        arg.ReplyWith($"ERROR: Failed to set {option}");
                        return;
                    }
                    break;
                default:
                    arg.ReplyWith($"ERROR: Unknown option '{option}'");
                    return;
            }

            arg.ReplyWith("OK:updated");
        }

        private void SetInfoProperty(object info, string option, string value)
        {
            var type = info.GetType();

            // Map our option names to HumanNPCInfo field names
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
                    // respawn value may be "true 60" (bool + seconds)
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

            // Find the NPC
            var humanPlayer = HumanNPC.Call("FindHumanPlayerByID", npcId);
            if (humanPlayer == null)
            {
                arg.ReplyWith("ERROR: NPC not found");
                return;
            }

            var npcPlayer = (humanPlayer as MonoBehaviour)?.GetComponent<BasePlayer>();
            if (npcPlayer == null)
            {
                arg.ReplyWith("ERROR: Could not access NPC player");
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

        private class NpcInfo
        {
            public ulong Id;
            public string Name;
            public float X, Y, Z;
            public float Health;
        }

        private List<NpcInfo> GetNpcList()
        {
            var result = new List<NpcInfo>();

            foreach (var player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                if (player.IsNpc && player.UserIDString.Length >= 17)
                {
                    var humanPlayer = HumanNPC?.Call("FindHumanPlayerByID", player.userID);
                    if (humanPlayer == null) continue;

                    result.Add(new NpcInfo
                    {
                        Id = player.userID,
                        Name = player.displayName ?? "NPC",
                        X = player.transform.position.x,
                        Y = player.transform.position.y,
                        Z = player.transform.position.z,
                        Health = player.health
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
