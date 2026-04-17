import { layout } from "./layout";
import { pageHeader, button, escapeHtml } from "./components";

type Cmd = [command: string, label: string];
type Group = { title: string; cmds: Cmd[] };

const GROUPS: Group[] = [
  {
    title: "Server Info",
    cmds: [
      ["status", "Status"],
      ["serverinfo", "Server Info"],
      ["playerlist", "Player List"],
      ["users", "Connected Users"],
      ["sleepingusers", "Sleeping Users"],
      ["server.fps", "Server FPS"],
      ["fps", "Client FPS"],
      ["global.perf 1", "Perf 1"],
      ["global.perf 6", "Perf 6"],
      ["server.seed", "Map Seed"],
      ["server.worldsize", "Map Size"],
      ["server.level", "Map Level"],
      ["server.tickrate", "Tickrate"],
      ["server.tags", "Server Tags"],
      ["version", "Version"],
    ],
  },
  {
    title: "Server Control",
    cmds: [
      ["server.save", "Save World"],
      ["server.writecfg", "Write Config"],
      ["global.writecfg", "Write Global Cfg"],
      ['restart 60 "Server restarting in 1 minute"', "Restart (1m)"],
      ['restart 300 "Server restarting in 5 minutes"', "Restart (5m)"],
      ['restart 600 "Server restarting in 10 minutes"', "Restart (10m)"],
      ['restart <seconds> "<message>"', "Restart (custom)"],
      ["quit", "Stop Server"],
      ["reloadbans", "Reload Bans"],
    ],
  },
  {
    title: "Server Config",
    cmds: [
      ['server.hostname "<name>"', "Set Hostname"],
      ['server.description "<text>"', "Set Description"],
      ['server.url "<url>"', "Set URL"],
      ['server.headerimage "<url>"', "Set Header Image"],
      ['server.motd "<text>"', "Set MOTD"],
      ["server.maxplayers <n>", "Set Max Players"],
      ["server.pve true", "PVE On"],
      ["server.pve false", "PVE Off"],
      ["server.globalchat true", "Global Chat On"],
      ["server.globalchat false", "Global Chat Off"],
      ["server.stability true", "Stability On"],
      ["server.stability false", "Stability Off"],
      ["server.radiation true", "Radiation On"],
      ["server.radiation false", "Radiation Off"],
      ["server.tickrate 30", "Tickrate 30"],
      ["server.saveinterval 300", "Save Interval 5m"],
      ["server.queryport 28017", "Query Port 28017"],
    ],
  },
  {
    title: "World & Time",
    cmds: [
      ["env.time", "Current Time"],
      ["env.time 6", "Sunrise (06:00)"],
      ["env.time 9", "Morning (09:00)"],
      ["env.time 12", "Noon (12:00)"],
      ["env.time 18", "Sunset (18:00)"],
      ["env.time 22", "Night (22:00)"],
      ["env.time 0", "Midnight (00:00)"],
      ["env.progresstime true", "Time Progress On"],
      ["env.progresstime false", "Time Progress Off"],
    ],
  },
  {
    title: "Weather",
    cmds: [
      ["weather.load", "Reload Weather"],
      ["weather.fog 0", "Fog 0%"],
      ["weather.fog 1", "Fog 100%"],
      ["weather.rain 0", "Rain 0%"],
      ["weather.rain 1", "Rain 100%"],
      ["weather.clouds 0", "Clouds 0%"],
      ["weather.clouds 1", "Clouds 100%"],
      ["weather.wind 0", "Wind 0%"],
      ["weather.wind 1", "Wind 100%"],
      ["weather.thunder 0", "Thunder 0%"],
      ["weather.thunder 1", "Thunder 100%"],
    ],
  },
  {
    title: "Events",
    cmds: [
      ["event.run", "Run Random Event"],
      ["airdrop", "Airdrop"],
      ["supply.drop", "Supply Drop"],
      ["spawn patrolhelicopter", "Spawn Heli"],
      ["spawn cargoshiptest", "Spawn Cargo Ship"],
      ["spawn ch47scientists.entity", "Spawn Chinook"],
      ["spawn bradleyapc", "Spawn Bradley"],
      ["bradley.spawn", "Bradley Event"],
      ["heli.call", "Call Heli"],
      ["cargoship.event_enabled true", "Cargo Events On"],
      ["cargoship.event_enabled false", "Cargo Events Off"],
      ["patrolhelicopterai_enabled true", "Heli AI On"],
      ["patrolhelicopterai_enabled false", "Heli AI Off"],
    ],
  },
  {
    title: "Bans & Moderation",
    cmds: [
      ["banlistex", "Ban List (ext.)"],
      ["banlist", "Ban List"],
      ['banid <steamid> "<name>" "<reason>"', "Ban by SteamID"],
      ['ban "<player>" "<reason>"', "Ban by Name"],
      ["unban <steamid>", "Unban"],
      ['kick "<player>" "<reason>"', "Kick Player"],
      ["mute <steamid> <minutes>", "Mute Player"],
      ["unmute <steamid>", "Unmute Player"],
      ['moderatorid <steamid> "<name>" 0', "Add Moderator"],
      ["removemoderator <steamid>", "Remove Moderator"],
      ['ownerid <steamid> "<name>" 0', "Add Owner"],
      ["removeowner <steamid>", "Remove Owner"],
      ["writecfg", "Save Bans/Mods"],
    ],
  },
  {
    title: "Teleport",
    cmds: [
      ['teleport "<player>"', "Teleport to Player"],
      ['teleport2me "<player>"', "Teleport to Me"],
      ['teleport2death "<player>"', "Teleport to Death"],
      ["teleport2owner", "Teleport to Owner"],
      ["teleport2marker", "Teleport to Marker"],
      ['teleportpos "(<x>,<y>,<z>)"', "Teleport to Pos"],
      ["teleportany <entity>", "Teleport to Entity"],
    ],
  },
  {
    title: "Chat & Broadcast",
    cmds: [
      ['say "<message>"', "Broadcast"],
      ['say "Hello everyone!"', "Say Hello"],
      ['say "Server is restarting soon!"', "Warn Restart"],
      ['global.say "<message>"', "Global Say"],
      ['chat.say "<message>"', "Chat Say"],
    ],
  },
  {
    title: "Items & Inventory",
    cmds: [
      ["inventory.give <item> <amount>", "Give Item (self)"],
      ['inventory.giveto "<player>" <item> <amount>', "Give to Player"],
      ["inventory.giveid <player> <itemid> <amount>", "Give by ItemID"],
      ["inventory.giveall <item>", "Give to All"],
    ],
  },
  {
    title: "Oxide / uMod",
    cmds: [
      ["oxide.version", "Oxide Version"],
      ["oxide.plugins", "List Plugins"],
      ["plugins", "Plugins"],
      ["oxide.reload *", "Reload All"],
      ["oxide.reload <plugin>", "Reload Plugin"],
      ["oxide.load *", "Load All"],
      ["oxide.load <plugin>", "Load Plugin"],
      ["oxide.unload *", "Unload All"],
      ["oxide.unload <plugin>", "Unload Plugin"],
    ],
  },
  {
    title: "Permissions & Groups",
    cmds: [
      ["oxide.show perms", "Show Perms"],
      ["oxide.show groups", "Show Groups"],
      ["oxide.show user <name>", "Show User"],
      ["oxide.show group <group>", "Show Group"],
      ["oxide.grant user <user> <perm>", "Grant User Perm"],
      ["oxide.grant group <group> <perm>", "Grant Group Perm"],
      ["oxide.revoke user <user> <perm>", "Revoke User Perm"],
      ["oxide.revoke group <group> <perm>", "Revoke Group Perm"],
      ["oxide.group add <group>", "Add Group"],
      ["oxide.group remove <group>", "Remove Group"],
      ["oxide.usergroup add <user> <group>", "Add User to Group"],
      ["oxide.usergroup remove <user> <group>", "Remove User from Group"],
      ["oxide.grant group default vanish.allow", "Grant default vanish"],
    ],
  },
  {
    title: "Gathering Rates",
    cmds: [
      ["gather.rate dispenser * 1", "Dispenser x1"],
      ["gather.rate dispenser * 2", "Dispenser x2"],
      ["gather.rate dispenser * 5", "Dispenser x5"],
      ["gather.rate dispenser * 10", "Dispenser x10"],
      ["gather.rate pickup * 1", "Pickup x1"],
      ["gather.rate pickup * 2", "Pickup x2"],
      ["gather.rate pickup * 5", "Pickup x5"],
      ["gather.rate quarry * 1", "Quarry x1"],
      ["gather.rate quarry * 2", "Quarry x2"],
      ["gather.rate quarry * 5", "Quarry x5"],
    ],
  },
  {
    title: "Crafting",
    cmds: [
      ["crafting.instant true", "Instant Craft On"],
      ["crafting.instant false", "Instant Craft Off"],
      ["crafting.timescale 1", "Craft Speed x1"],
      ["crafting.timescale 0.5", "Craft Speed x2"],
      ["crafting.timescale 0.1", "Craft Speed x10"],
    ],
  },
  {
    title: "Decay & Stability",
    cmds: [
      ["decay.scale 0", "Decay Off"],
      ["decay.scale 0.5", "Decay x0.5"],
      ["decay.scale 1", "Decay x1"],
      ["decay.upkeep true", "Upkeep On"],
      ["decay.upkeep false", "Upkeep Off"],
      ["server.stability true", "Stability On"],
      ["server.stability false", "Stability Off"],
      ["building.deployableaboveground true", "Deploy Above On"],
      ["building.deployableaboveground false", "Deploy Above Off"],
    ],
  },
  {
    title: "Anti-cheat",
    cmds: [
      ["antihack.enabled true", "Anti-cheat On"],
      ["antihack.enabled false", "Anti-cheat Off"],
      ["antihack.noclip_protection 1", "Noclip Protect On"],
      ["antihack.noclip_protection 0", "Noclip Protect Off"],
      ["antihack.speedhack_protection 1", "Speedhack Protect On"],
      ["antihack.speedhack_protection 0", "Speedhack Protect Off"],
      ["antihack.flyhack_protection 1", "Flyhack Protect On"],
      ["antihack.flyhack_protection 0", "Flyhack Protect Off"],
    ],
  },
  {
    title: "Performance",
    cmds: [
      ["gc.collect", "Force GC"],
      ["pool.status", "Pool Status"],
      ["pool.clear_memory 0", "Clear Pool"],
      ["entity.stats", "Entity Stats"],
      ["entity.count", "Entity Count"],
      ["global.perf 1", "Perf View 1"],
      ["global.perf 6", "Perf View 6"],
      ["budget.fixedupdate", "Fixed Update Budget"],
      ["net.log 0", "Net Log Off"],
      ["net.log 1", "Net Log On"],
    ],
  },
  {
    title: "Admin & Debug",
    cmds: [
      ["find .", "All Commands"],
      ["find server", "Find 'server'"],
      ["find oxide", "Find 'oxide'"],
      ["find env", "Find 'env'"],
      ["find weather", "Find 'weather'"],
      ["find antihack", "Find 'antihack'"],
      ["convar.find <query>", "Find Convar"],
      ["server.identity", "Server Identity"],
    ],
  },
];

function renderGroup(group: Group): string {
  const buttons = group.cmds
    .map(([cmd, label]) => {
      const cmdAttr = escapeHtml(cmd);
      const needsInput = cmd.includes("<") ? "true" : "false";
      const haystack = escapeHtml(`${cmd} ${label} ${group.title}`.toLowerCase());
      return `<button data-cmd="${cmdAttr}" data-label="${escapeHtml(label)}" data-prefill="${needsInput}" data-search="${haystack}" class="cmd-btn inline-flex items-center rounded-lg border border-zinc-200 bg-white px-3 py-1.5 text-xs font-medium text-zinc-600 shadow-sm hover:bg-zinc-50 hover:text-zinc-900 transition-colors cursor-pointer" title="${cmdAttr}">${escapeHtml(label)}</button>`;
    })
    .join("\n        ");
  const titleSearch = escapeHtml(group.title.toLowerCase());
  return `<div class="cmd-group" data-group="${titleSearch}">
    <div class="text-xs font-medium text-zinc-400 uppercase tracking-wider mb-2">${escapeHtml(group.title)}</div>
    <div class="flex flex-wrap gap-1.5">
      ${buttons}
    </div>
  </div>`;
}

export function rconPage() {
  const total = GROUPS.reduce((n, g) => n + g.cmds.length, 0);
  const groupsHtml = GROUPS.map(renderGroup).join("\n");

  return layout("RCON Console", `
    ${pageHeader("Console", { description: "Execute RCON commands on the server" })}

    <div class="rounded-xl border border-zinc-200 bg-white shadow-sm overflow-hidden mb-6">
      <div id="output" class="console-output bg-zinc-950 text-zinc-300 p-4 h-96 overflow-y-auto whitespace-pre-wrap"></div>
      <form id="rcon-form" class="flex items-center border-t border-zinc-200 bg-white">
        <span class="text-zinc-400 pl-4 text-sm font-mono">&gt;</span>
        <input id="cmd" type="text" autocomplete="off" placeholder="Enter RCON command..."
          class="flex-1 bg-transparent text-sm text-zinc-900 px-3 py-3 focus:outline-none placeholder:text-zinc-400" />
        ${button("Send", { variant: "primary", size: "sm", type: "submit", class: "mr-2" })}
      </form>
    </div>

    <div class="mb-5 flex items-center gap-3">
      <div class="relative flex-1">
        <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-4.35-4.35M17 10a7 7 0 11-14 0 7 7 0 0114 0z"/></svg>
        <input id="rcon-search" type="search" autocomplete="off" placeholder="Search commands... (press / or ⌘K)"
          class="flex h-9 w-full rounded-lg border border-zinc-300 bg-white pl-9 pr-20 py-1.5 text-sm text-zinc-900 shadow-sm placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent" />
        <kbd id="rcon-search-kbd" class="absolute right-3 top-1/2 -translate-y-1/2 text-[10px] font-mono text-zinc-400 bg-zinc-100 border border-zinc-200 rounded px-1.5 py-0.5">/</kbd>
      </div>
      <div id="rcon-count" class="text-xs text-zinc-500 whitespace-nowrap">${total} commands</div>
    </div>

    <div id="cmd-groups" class="space-y-4">
      ${groupsHtml}
    </div>

    <div id="cmd-empty" class="hidden py-12 text-center text-sm text-zinc-500">
      No commands match your search.
    </div>

    <script>
      const output = document.getElementById('output');
      const form = document.getElementById('rcon-form');
      const cmd = document.getElementById('cmd');
      const search = document.getElementById('rcon-search');
      const countEl = document.getElementById('rcon-count');
      const emptyEl = document.getElementById('cmd-empty');
      const groupsEl = document.getElementById('cmd-groups');
      const history = [];
      let histIdx = -1;

      function appendOutput(text, isCmd) {
        const line = document.createElement('div');
        if (isCmd) {
          line.className = 'text-amber-400 mt-2 font-medium';
          line.textContent = '> ' + text;
        } else {
          line.className = 'text-zinc-400';
          line.textContent = text;
        }
        output.appendChild(line);
        output.scrollTop = output.scrollHeight;
      }

      async function sendCommand(command) {
        appendOutput(command, true);
        try {
          const res = await fetch('/api/rcon', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ command })
          });
          const data = await res.json();
          if (data.error) {
            appendOutput('Error: ' + data.error, false);
          } else {
            appendOutput(data.response || '(no output)', false);
          }
        } catch (e) {
          appendOutput('Error: ' + e.message, false);
        }
      }

      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const command = cmd.value.trim();
        if (!command) return;
        history.unshift(command);
        histIdx = -1;
        cmd.value = '';
        await sendCommand(command);
      });

      cmd.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowUp') {
          e.preventDefault();
          if (histIdx < history.length - 1) { histIdx++; cmd.value = history[histIdx]; }
        } else if (e.key === 'ArrowDown') {
          e.preventDefault();
          if (histIdx > 0) { histIdx--; cmd.value = history[histIdx]; }
          else { histIdx = -1; cmd.value = ''; }
        }
      });

      document.querySelectorAll('.cmd-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          const command = btn.dataset.cmd;
          const prefill = btn.dataset.prefill === 'true';
          if (prefill) {
            cmd.value = command;
            cmd.focus();
            const firstPlaceholder = command.indexOf('<');
            if (firstPlaceholder >= 0) {
              const end = command.indexOf('>', firstPlaceholder);
              cmd.setSelectionRange(firstPlaceholder, end + 1);
            }
          } else {
            sendCommand(command);
          }
        });
      });

      // Search / filter
      function applyFilter() {
        const q = search.value.trim().toLowerCase();
        let visible = 0;
        document.querySelectorAll('.cmd-group').forEach(group => {
          let groupMatches = 0;
          const groupTitle = group.dataset.group || '';
          const groupTitleMatches = q && groupTitle.includes(q);
          group.querySelectorAll('.cmd-btn').forEach(btn => {
            const hay = btn.dataset.search || '';
            const match = !q || groupTitleMatches || hay.includes(q);
            btn.style.display = match ? '' : 'none';
            if (match) { groupMatches++; visible++; }
          });
          group.style.display = groupMatches > 0 ? '' : 'none';
        });
        countEl.textContent = q
          ? visible + ' / ${total} commands'
          : '${total} commands';
        emptyEl.classList.toggle('hidden', visible > 0);
        groupsEl.classList.toggle('hidden', visible === 0);
      }

      search.addEventListener('input', applyFilter);
      search.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') { search.value = ''; applyFilter(); search.blur(); }
      });

      // Keyboard shortcut: / or Cmd/Ctrl+K focuses search
      document.addEventListener('keydown', (e) => {
        const target = e.target;
        const isTyping = target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable);
        if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
          e.preventDefault();
          search.focus();
          search.select();
          return;
        }
        if (e.key === '/' && !isTyping) {
          e.preventDefault();
          search.focus();
          search.select();
        }
      });

      // Show appropriate shortcut hint based on platform
      const isMac = /Mac|iPhone|iPad|iPod/.test(navigator.platform);
      const kbd = document.getElementById('rcon-search-kbd');
      if (kbd) kbd.textContent = isMac ? '\u2318K' : 'Ctrl+K';
    </script>
  `, { activePage: "rcon" });
}
