using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NpcAdmin", "rust-gg", "1.0.0")]
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

            var result = HumanNPC.Call("SpawnHumanNPC", position, rotation, npcName, (ulong)0);
            if (result == null)
            {
                arg.ReplyWith("ERROR: Failed to spawn NPC");
                return;
            }

            arg.ReplyWith($"OK:{result}");
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

            HumanNPC.Call("RemoveHumanNPC", npcId);
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
                HumanNPC.Call("RemoveHumanNPC", npc.Id);
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

            string option = arg.Args[1];
            string value = string.Join(" ", arg.Args.Skip(2));

            HumanNPC.Call("SetHumanNPCInfo", npcId, option, value);
            arg.ReplyWith("OK:updated");
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

            string item = arg.Args[1];
            string loc = arg.Args.Length >= 3 ? arg.Args[2] : "belt";

            HumanNPC.Call("GiveHumanNPC", npcId, item, loc);
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
                    // Check if this NPC belongs to HumanNPC by querying its info
                    var info = HumanNPC?.Call("GetHumanNPCInfo", player.userID);
                    if (info == null) continue;

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
