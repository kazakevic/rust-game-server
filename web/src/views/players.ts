import { layout } from "./layout";
import { pageHeader, card, button, emptyState } from "./components";

export function playersPage() {
  return layout("Players", `
    ${pageHeader("Players", {
      description: "View online players and manage their positions",
      actions: button("Refresh", { variant: "outline", size: "sm", attrs: 'id="refresh-btn"' }),
    })}

    <div id="error-banner" class="hidden mb-4"></div>
    <div id="success-banner" class="hidden mb-4"></div>

    ${card({ title: "Online Players", description: "Players currently connected to the server", headerRight: `
      <div id="player-count" class="text-sm text-zinc-400"></div>
    ` }, `
      <div id="players-loading" class="py-8 text-center text-sm text-zinc-400">Loading players...</div>
      <div id="players-empty" class="hidden"></div>
      <div id="players-table" class="hidden">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-zinc-100">
              <th class="text-left py-3 px-4 font-medium text-zinc-500">Player</th>
              <th class="text-left py-3 px-4 font-medium text-zinc-500">Steam ID</th>
              <th class="text-right py-3 px-4 font-medium text-zinc-500">Actions</th>
            </tr>
          </thead>
          <tbody id="players-body"></tbody>
        </table>
      </div>
    `)}

    <script>
      async function loadPlayers() {
        const loading = document.getElementById('players-loading');
        const emptyEl = document.getElementById('players-empty');
        const tableEl = document.getElementById('players-table');
        const tbody = document.getElementById('players-body');
        const countEl = document.getElementById('player-count');

        loading.classList.remove('hidden');
        emptyEl.classList.add('hidden');
        tableEl.classList.add('hidden');

        try {
          const res = await fetch('/api/players');
          const data = await res.json();
          const players = data.players || [];

          loading.classList.add('hidden');

          if (players.length === 0) {
            countEl.textContent = '0 online';
            emptyEl.classList.remove('hidden');
            emptyEl.innerHTML = \`<div class="py-12 text-center">
              <p class="text-sm text-zinc-400">No players online</p>
            </div>\`;
            return;
          }

          countEl.textContent = players.length + ' online';
          tbody.innerHTML = players.map(p => \`
            <tr class="border-b border-zinc-50 hover:bg-zinc-50 transition-colors">
              <td class="py-3 px-4 font-medium text-zinc-900">\${escHtml(p.DisplayName)}</td>
              <td class="py-3 px-4 text-zinc-500 font-mono text-xs">\${escHtml(p.SteamID)}</td>
              <td class="py-3 px-4 text-right">
                <button
                  onclick="teleportTo('\${escHtml(p.SteamID)}', '\${escHtml(p.DisplayName)}')"
                  class="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium bg-zinc-900 text-white hover:bg-zinc-700 transition-colors cursor-pointer"
                >
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
                  </svg>
                  Teleport to
                </button>
              </td>
            </tr>
          \`).join('');
          tableEl.classList.remove('hidden');
        } catch (e) {
          loading.classList.add('hidden');
          showError('Failed to load players: ' + e.message);
        }
      }

      async function teleportTo(steamId, name) {
        hideAlerts();
        try {
          const res = await fetch('/api/players/teleport', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ steamId }),
          });
          const data = await res.json();
          if (data.error) {
            showError('Teleport failed: ' + data.error);
          } else {
            showSuccess('Teleported to ' + name);
          }
        } catch (e) {
          showError('Teleport failed: ' + e.message);
        }
      }

      function showError(msg) {
        const el = document.getElementById('error-banner');
        el.className = 'mb-4 flex items-start gap-3 rounded-lg border px-4 py-3 text-sm bg-red-50 border-red-200 text-red-800';
        el.innerHTML = '<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" stroke-width="2"/><path stroke-width="2" d="M12 8v4m0 4h.01"/></svg><span>' + msg + '</span>';
      }

      function showSuccess(msg) {
        const el = document.getElementById('success-banner');
        el.className = 'mb-4 flex items-start gap-3 rounded-lg border px-4 py-3 text-sm bg-emerald-50 border-emerald-200 text-emerald-800';
        el.innerHTML = '<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg><span>' + msg + '</span>';
      }

      function hideAlerts() {
        document.getElementById('error-banner').classList.add('hidden');
        document.getElementById('success-banner').classList.add('hidden');
      }

      function escHtml(s) {
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
      }

      document.getElementById('refresh-btn').addEventListener('click', loadPlayers);

      loadPlayers();
    </script>
  `, { activePage: "players" });
}
