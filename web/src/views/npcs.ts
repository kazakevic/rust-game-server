import { layout } from "./layout";
import { pageHeader, section, button, input, select, checkbox, alert, card, badge, emptyState, modal } from "./components";

export function npcsPage(opts?: { success?: string; error?: string }) {
  return layout("NPC Manager", `
    ${opts?.success ? alert(opts.success, "success") + '<div class="mb-4"></div>' : ""}
    ${opts?.error ? alert(opts.error, "error") + '<div class="mb-4"></div>' : ""}

    ${pageHeader("NPC Manager", { description: "Spawn and manage Human NPCs on the server", actions: `
      ${button("Reload Plugin", { variant: "outline", size: "sm", attrs: 'id="reload-plugin-btn" title="Reload NpcAdmin plugin on the server"' })}
    ` })}

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
      <div class="lg:col-span-2">
        ${card({ title: "Spawn NPC", description: "Create a new NPC near a selected online player" }, `
          <div id="spawn-error" class="hidden mb-4"></div>
          <div id="spawn-success" class="hidden mb-4"></div>

          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-zinc-700 mb-1.5">Target Player</label>
              <div class="flex items-center gap-2">
                <select id="player-select"
                  class="flex-1 h-9 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent cursor-pointer">
                  <option value="">Loading players...</option>
                </select>
                ${button("Refresh", { variant: "outline", size: "sm", attrs: 'id="refresh-players" title="Refresh player list"' })}
              </div>
              <p class="mt-1 text-xs text-zinc-400">NPC will spawn 3m in front of the selected player</p>
            </div>

            ${input({ name: "npc-name", label: "NPC Name", value: "Guard", placeholder: "Enter NPC name..." })}

            <div class="grid grid-cols-2 gap-4">
              ${input({ name: "npc-health", label: "Health", type: "number", value: "100", hint: "Max 100 in vanilla Rust" })}
              ${input({ name: "npc-radius", label: "Detection Radius", type: "number", value: "30", hint: "Range to detect players" })}
            </div>

            <div class="grid grid-cols-2 gap-4">
              ${input({ name: "npc-damage", label: "Damage Amount", type: "number", value: "10" })}
              ${input({ name: "npc-speed", label: "Speed", type: "number", value: "3", step: "0.5" })}
            </div>

            ${input({ name: "npc-kit", label: "Kit (optional)", placeholder: "Kit name from Kits plugin", hint: "Leave empty for default loadout" })}

            <div class="flex flex-wrap gap-x-6 gap-y-3">
              ${checkbox({ name: "npc-hostile", label: "Hostile (attacks players on sight)", checked: true })}
              ${checkbox({ name: "npc-invulnerable", label: "Invulnerable" })}
              ${checkbox({ name: "npc-lootable", label: "Lootable on death", checked: true })}
              ${checkbox({ name: "npc-respawn", label: "Respawn after death" })}
            </div>

            <div id="respawn-opts" class="hidden">
              ${input({ name: "npc-respawn-time", label: "Respawn Delay (seconds)", type: "number", value: "60" })}
            </div>

            <div class="flex items-center gap-2 pt-2">
              ${button("Spawn NPC", { variant: "primary", size: "lg", attrs: 'id="spawn-btn"' })}
            </div>
          </div>
        `)}
      </div>

      <div>
        ${card({ title: "Quick Presets", description: "One-click spawn common NPC types" }, `
          <div class="space-y-2">
            ${presetButton("Hostile Guard", { hostile: true, health: "100", damage: "15", radius: "40", speed: "3.5" })}
            ${presetButton("Passive Trader", { hostile: false, health: "100", damage: "0", radius: "10", speed: "0", invulnerable: true })}
            ${presetButton("Patrol Soldier", { hostile: true, health: "150", damage: "20", radius: "50", speed: "4" })}
            ${presetButton("Weak Zombie", { hostile: true, health: "50", damage: "8", radius: "20", speed: "2.5" })}
          </div>
        `)}
      </div>
    </div>

    ${card({ title: "Active NPCs", description: "Currently spawned Human NPCs", headerRight: `
      <div class="flex items-center gap-2">
        ${button("Refresh", { variant: "outline", size: "sm", attrs: 'id="refresh-npcs"' })}
        ${button("Remove All", { variant: "destructive", size: "sm", attrs: 'id="remove-all-btn"' })}
      </div>
    ` }, `
      <div id="npc-list">
        ${emptyState("Loading NPCs...")}
      </div>
    `)}

    ${modal("confirm-remove-all", "Remove All NPCs", `
      <p class="text-sm text-zinc-600 mb-4">Are you sure you want to remove <strong>all</strong> Human NPCs from the server? This cannot be undone.</p>
      <div class="flex justify-end gap-2">
        ${button("Cancel", { variant: "outline", size: "sm", attrs: 'onclick="document.getElementById(\'confirm-remove-all\').classList.add(\'hidden\')"' })}
        ${button("Remove All", { variant: "destructive", size: "sm", attrs: 'id="confirm-remove-all-btn"' })}
      </div>
    `)}

    <script>
      const playerSelect = document.getElementById('player-select');
      const spawnBtn = document.getElementById('spawn-btn');
      const refreshPlayersBtn = document.getElementById('refresh-players');
      const refreshNpcsBtn = document.getElementById('refresh-npcs');
      const removeAllBtn = document.getElementById('remove-all-btn');
      const reloadPluginBtn = document.getElementById('reload-plugin-btn');
      const confirmRemoveAllBtn = document.getElementById('confirm-remove-all-btn');
      const npcListEl = document.getElementById('npc-list');
      const spawnError = document.getElementById('spawn-error');
      const spawnSuccess = document.getElementById('spawn-success');

      // Toggle respawn options
      const respawnCheck = document.querySelector('input[name="npc-respawn"][type="checkbox"]');
      const respawnOpts = document.getElementById('respawn-opts');
      respawnCheck.addEventListener('change', () => {
        respawnOpts.classList.toggle('hidden', !respawnCheck.checked);
      });

      function showMsg(el, msg, variant) {
        const styles = {
          error: 'bg-red-50 border-red-200 text-red-800',
          success: 'bg-emerald-50 border-emerald-200 text-emerald-800'
        };
        el.className = 'flex items-start gap-3 rounded-lg border px-4 py-3 text-sm mb-4 ' + styles[variant];
        el.textContent = msg;
        el.classList.remove('hidden');
        setTimeout(() => el.classList.add('hidden'), 5000);
      }

      async function rcon(cmd) {
        const res = await fetch('/api/rcon', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ command: cmd })
        });
        const data = await res.json();
        if (data.error) throw new Error(data.error);
        return data.response || '';
      }

      async function loadPlayers() {
        playerSelect.innerHTML = '<option value="">Loading...</option>';
        try {
          const raw = await rcon('playerlist');
          const players = JSON.parse(raw);
          if (!players.length) {
            playerSelect.innerHTML = '<option value="">No players online</option>';
            return;
          }
          playerSelect.innerHTML = players
            .map(p => '<option value="' + p.SteamID + '">' + (p.DisplayName || p.SteamID) + '</option>')
            .join('');
        } catch (e) {
          playerSelect.innerHTML = '<option value="">Failed to load players</option>';
        }
      }

      async function loadNpcs() {
        npcListEl.innerHTML = '<div class="text-sm text-zinc-400 py-4 text-center">Loading...</div>';
        try {
          const raw = await rcon('npcadmin.list');
          const npcs = JSON.parse(raw);
          if (!npcs.length) {
            npcListEl.innerHTML = '<div class="flex flex-col items-center justify-center py-8 text-center"><svg class="w-8 h-8 text-zinc-300 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/></svg><p class="text-sm text-zinc-500">No NPCs spawned</p></div>';
            return;
          }
          npcListEl.innerHTML = '<div class="divide-y divide-zinc-100">' +
            npcs.map(npc =>
              '<div class="flex items-center justify-between py-3' + (npc.Online ? '' : ' opacity-60') + '">' +
                '<div class="flex items-center gap-3">' +
                  '<div class="flex items-center justify-center w-8 h-8 rounded-full ' + (npc.Online ? 'bg-zinc-100 text-zinc-500' : 'bg-zinc-50 text-zinc-400') + ' text-xs font-mono">' + String(npc.Id).slice(-4) + '</div>' +
                  '<div>' +
                    '<p class="text-sm font-medium text-zinc-900">' + escapeHtml(npc.Name) +
                      (npc.Online
                        ? ' <span class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">Online</span>'
                        : ' <span class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-zinc-100 text-zinc-500 border border-zinc-200">Offline</span>') +
                      (npc.Invulnerable ? ' <span class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">Invulnerable</span>' : '') +
                      (npc.Hostile ? ' <span class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-red-50 text-red-700 border border-red-200">Hostile</span>' : '') +
                    '</p>' +
                    '<p class="text-xs text-zinc-400">ID: ' + npc.Id + ' &middot; HP: ' + Math.round(npc.Health) +
                      (npc.Kit ? ' &middot; Kit: ' + escapeHtml(npc.Kit) : '') +
                      (npc.Online ? ' &middot; Pos: ' + Math.round(npc.X) + ', ' + Math.round(npc.Y) + ', ' + Math.round(npc.Z) : '') +
                    '</p>' +
                  '</div>' +
                '</div>' +
                '<button onclick="removeNpc(' + npc.Id + ')" class="inline-flex items-center rounded-lg border border-red-200 bg-red-50 px-2.5 py-1 text-xs font-medium text-red-600 hover:bg-red-100 transition-colors cursor-pointer">Remove</button>' +
              '</div>'
            ).join('') +
          '</div>';
        } catch (e) {
          npcListEl.innerHTML = '<div class="text-sm text-red-500 py-4 text-center">Failed to load NPCs: ' + escapeHtml(e.message) + '</div>';
        }
      }

      function escapeHtml(s) {
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
      }

      async function removeNpc(id) {
        if (!confirm('Remove NPC ' + id + '?')) return;
        try {
          const res = await rcon('npcadmin.remove ' + id);
          if (res.startsWith('ERROR')) throw new Error(res);
          loadNpcs();
        } catch (e) {
          alert('Failed to remove NPC: ' + e.message);
        }
      }

      // Spawn
      spawnBtn.addEventListener('click', async () => {
        const steamId = playerSelect.value;
        if (!steamId) { showMsg(spawnError, 'Please select a player first.', 'error'); return; }

        const name = document.querySelector('input[name="npc-name"]').value.trim() || 'NPC';
        spawnBtn.disabled = true;
        spawnBtn.textContent = 'Spawning...';

        try {
          const res = await rcon('npcadmin.spawn ' + steamId + ' "' + name.replace(/"/g, '') + '"');
          if (res.startsWith('ERROR')) throw new Error(res.replace('ERROR: ', ''));

          const npcId = res.replace('OK:', '');

          // Apply settings
          const settings = [];
          const health = document.querySelector('input[name="npc-health"]').value;
          const radius = document.querySelector('input[name="npc-radius"]').value;
          const damage = document.querySelector('input[name="npc-damage"]').value;
          const speed = document.querySelector('input[name="npc-speed"]').value;
          const kit = document.querySelector('input[name="npc-kit"]').value.trim();
          const hostile = document.querySelector('input[name="npc-hostile"][type="checkbox"]').checked;
          const invuln = document.querySelector('input[name="npc-invulnerable"][type="checkbox"]').checked;
          const lootable = document.querySelector('input[name="npc-lootable"][type="checkbox"]').checked;
          const respawn = document.querySelector('input[name="npc-respawn"][type="checkbox"]').checked;

          if (health) settings.push(['health', health]);
          if (radius) settings.push(['radius', radius]);
          if (damage) settings.push(['damageamount', damage]);
          if (speed) settings.push(['speed', speed]);
          if (kit) settings.push(['kit', kit]);
          settings.push(['hostile', hostile ? 'true' : 'false']);
          settings.push(['invulnerable', invuln ? 'true' : 'false']);
          settings.push(['lootable', lootable ? 'true' : 'false']);
          if (respawn) {
            const respawnTime = document.querySelector('input[name="npc-respawn-time"]').value || '60';
            settings.push(['respawn', 'true ' + respawnTime]);
          }

          for (const [key, val] of settings) {
            await rcon('npcadmin.set ' + npcId + ' ' + key + ' ' + val);
          }

          showMsg(spawnSuccess, 'NPC "' + name + '" spawned successfully (ID: ' + npcId + ')', 'success');
          loadNpcs();
        } catch (e) {
          showMsg(spawnError, 'Failed to spawn NPC: ' + e.message, 'error');
        } finally {
          spawnBtn.disabled = false;
          spawnBtn.textContent = 'Spawn NPC';
        }
      });

      // Presets
      document.querySelectorAll('[data-preset]').forEach(btn => {
        btn.addEventListener('click', () => {
          const p = JSON.parse(btn.dataset.preset);
          if (p.name) document.querySelector('input[name="npc-name"]').value = p.name;
          if (p.health) document.querySelector('input[name="npc-health"]').value = p.health;
          if (p.damage) document.querySelector('input[name="npc-damage"]').value = p.damage;
          if (p.radius) document.querySelector('input[name="npc-radius"]').value = p.radius;
          if (p.speed) document.querySelector('input[name="npc-speed"]').value = p.speed;
          document.querySelector('input[name="npc-hostile"][type="checkbox"]').checked = !!p.hostile;
          document.querySelector('input[name="npc-invulnerable"][type="checkbox"]').checked = !!p.invulnerable;
        });
      });

      // Remove all
      removeAllBtn.addEventListener('click', () => {
        document.getElementById('confirm-remove-all').classList.remove('hidden');
      });

      confirmRemoveAllBtn.addEventListener('click', async () => {
        document.getElementById('confirm-remove-all').classList.add('hidden');
        try {
          const res = await rcon('npcadmin.removeall');
          if (res.startsWith('ERROR')) throw new Error(res);
          const count = res.replace('OK:', '');
          showMsg(spawnSuccess, 'Removed ' + count + ' NPC(s).', 'success');
          loadNpcs();
        } catch (e) {
          showMsg(spawnError, 'Failed to remove NPCs: ' + e.message, 'error');
        }
      });

      reloadPluginBtn.addEventListener('click', async () => {
        reloadPluginBtn.disabled = true;
        reloadPluginBtn.textContent = 'Reloading...';
        try {
          await rcon('oxide.reload NpcAdmin');
          await rcon('oxide.reload HumanNPC');
          showMsg(spawnSuccess, 'NpcAdmin and HumanNPC plugins reloaded.', 'success');
          setTimeout(loadNpcs, 1000);
        } catch (e) {
          showMsg(spawnError, 'Failed to reload: ' + e.message, 'error');
        } finally {
          reloadPluginBtn.disabled = false;
          reloadPluginBtn.textContent = 'Reload Plugin';
        }
      });

      refreshPlayersBtn.addEventListener('click', loadPlayers);
      refreshNpcsBtn.addEventListener('click', loadNpcs);

      // Initial load
      loadPlayers();
      loadNpcs();
    </script>
  `, { activePage: "npcs" });
}

function presetButton(label: string, opts: { hostile: boolean; health: string; damage: string; radius: string; speed: string; invulnerable?: boolean }) {
  const preset = JSON.stringify({ name: label, ...opts });
  return `<button data-preset='${preset.replace(/'/g, "&#39;")}'
    class="w-full flex items-center justify-between rounded-lg border border-zinc-200 bg-white px-4 py-3 text-sm text-zinc-700 shadow-sm hover:bg-zinc-50 hover:text-zinc-900 transition-colors cursor-pointer text-left">
    <span class="font-medium">${label}</span>
    <span class="text-xs text-zinc-400">${opts.hostile ? "Hostile" : "Passive"} &middot; HP ${opts.health}</span>
  </button>`;
}
