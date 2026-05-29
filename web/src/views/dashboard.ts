import { layout } from "./layout";
import { statsCard, statusDot, button, pageHeader, section } from "./components";

interface SchedulerState {
  enabled: boolean;
  intervalMin: number;
  delayMin: number;
  restartScheduled: boolean;
  restartAt: number | null;
  pendingBuild: string | null;
}

interface DashboardData {
  status: { running: boolean; status: string; startedAt: string };
  stats: { cpu: string; memoryUsed: string; memoryLimit: string; memoryPercent: string };
  serverInfo: { hostname: string; players: string; maxPlayers: string; map: string; fps: string };
  scheduler: SchedulerState;
}

export function dashboardPage(data: DashboardData) {
  const { status, stats, serverInfo, scheduler } = data;
  const uptime = status.running && status.startedAt ? timeSince(status.startedAt) : "N/A";
  const autoUpdateText = scheduler.enabled
    ? `Auto-update: on · checks every ${scheduler.intervalMin}m · ${scheduler.delayMin}m player warning before restart`
    : "Auto-update: off";

  // Both control groups are rendered; the visible one is toggled client-side from live status.
  const runningControls = `<div id="controls-running" class="flex items-center gap-2 ${status.running ? "" : "hidden"}">
      ${button("Check for updates", { variant: "outline", size: "sm", attrs: `id="check-updates-btn"` })}
      ${button("Restart", { variant: "warning", size: "sm", attrs: `data-action="restart" data-confirm="Restart the server?"` })}
      ${button("Wipe", { variant: "destructive", size: "sm", attrs: `data-action="wipe" data-confirm="⚠️ This deletes all map and save data, then restarts. Are you sure?"` })}
      ${button("Stop", { variant: "destructive", size: "sm", attrs: `data-action="stop" data-confirm="Stop the server?"` })}
    </div>`;

  const stoppedControls = `<div id="controls-stopped" class="${status.running ? "hidden" : ""}">
      ${button("Start Server", { variant: "success", size: "sm", attrs: `data-action="start"` })}
    </div>`;

  const statusValue = `<span class="flex items-center gap-2.5">
    ${statusDot(status.running)}
    <span class="${status.running ? "text-emerald-700" : "text-red-600"} font-medium">${status.status}</span>
  </span>`;

  return layout("Dashboard", `
    ${pageHeader("Dashboard", { actions: `${runningControls}${stoppedControls}` })}

    <div id="action-banner" class="hidden mb-4 flex items-center gap-3 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800">
      <svg class="animate-spin h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/></svg>
      <span id="action-banner-text"></span>
    </div>
    <div id="action-error" class="hidden mb-4 flex items-center gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
      <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" stroke-width="2"/><path stroke-width="2" d="M12 8v4m0 4h.01"/></svg>
      <span id="action-error-text"></span>
    </div>
    <div id="update-result" class="hidden mb-4 flex items-center gap-3 rounded-lg border px-4 py-3 text-sm">
      <span id="update-result-text"></span>
      <span id="update-result-action" class="ml-auto shrink-0">
        ${button("Update & restart", { variant: "warning", size: "sm", attrs: `id="do-update-btn" data-action="restart"`, class: "hidden" })}
      </span>
    </div>
    <div id="autoupdate-banner" class="hidden mb-4 flex items-center gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      <svg class="animate-spin h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/></svg>
      <span id="autoupdate-text"></span>
      ${button("Cancel", { variant: "outline", size: "sm", attrs: `id="cancel-update-btn"`, class: "ml-auto shrink-0" })}
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
      ${statsCard("Status", statusValue, { valueId: "tile-status" })}
      ${statsCard("Uptime", uptime, { valueId: "tile-uptime" })}
      ${statsCard("CPU", `${stats.cpu}%`, { valueId: "tile-cpu" })}
      ${statsCard("Memory", `${stats.memoryUsed} MB`, { valueId: "tile-mem", detail: `${stats.memoryPercent}% of ${stats.memoryLimit} MB`, detailId: "tile-mem-detail" })}
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      ${statsCard("Hostname", serverInfo.hostname || "N/A", { valueId: "tile-hostname" })}
      ${statsCard("Players", `${serverInfo.players || "0"} / ${serverInfo.maxPlayers || "0"}`, { valueId: "tile-players" })}
      ${statsCard("Map", serverInfo.map || "N/A", { valueId: "tile-map" })}
      ${statsCard("Server FPS", serverInfo.fps || "N/A", { valueId: "tile-fps" })}
    </div>

    <div id="live-controls" class="grid grid-cols-1 lg:grid-cols-3 gap-4 ${status.running ? "" : "hidden"}">
      ${section("Plugin Controls", `
        <div class="flex flex-wrap gap-2">
          <form method="POST" action="/api/plugins/reload-all">
            ${button("Reload All Plugins", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/plugins/redownload">
            ${button("Re-download Plugins", { variant: "outline", size: "sm", type: "submit", attrs: `onclick="this.disabled=true;this.textContent='Downloading...'"` })}
          </form>
        </div>
      `, { description: "Manage Oxide/uMod plugins" })}

      ${section("World Controls", `
        <div class="flex flex-wrap gap-2">
          <form method="POST" action="/api/world/set-day">
            ${button("Set Day", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/world/set-night">
            ${button("Set Night", { variant: "outline", size: "sm", type: "submit" })}
          </form>
        </div>
      `, { description: "Control in-game time" })}

      ${section("Weather Controls", `
        <div class="flex flex-wrap gap-2">
          <form method="POST" action="/api/weather/clear">
            ${button("Clear", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/weather/rain">
            ${button("Rain", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/weather/fog">
            ${button("Fog", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/weather/storm">
            ${button("Storm", { variant: "outline", size: "sm", type: "submit" })}
          </form>
        </div>
      `, { description: "Control weather effects" })}
    </div>

    <p id="autoupdate-status" class="mt-6 text-xs text-zinc-400">${autoUpdateText}</p>

    <script>
      // ── Live dashboard: poll status, update tiles in place, drive control buttons ──
      const PENDING = {
        start:   { label: 'Starting server…',                 done: (s) => s.status.running },
        stop:    { label: 'Stopping server… (saving world)',  done: (s) => !s.status.running },
        restart: { label: 'Restarting server… (saving world)', done: (s, p) => s.status.running && s.status.startedAt && s.status.startedAt !== p.prevStartedAt },
        wipe:    { label: 'Wiping map & restarting server…',  done: (s, p) => s.status.running && s.status.startedAt && s.status.startedAt !== p.prevStartedAt },
      };
      const POLL_MS = 5000;
      const PENDING_TIMEOUT_MS = 300000; // give a save/restart up to 5 min before giving up

      let pending = null;          // { type, prevStartedAt, deadline }
      let lastRunning = ${status.running};
      let lastStartedAt = ${JSON.stringify(status.startedAt || "")};
      let autoRestartAt = null, autoBuild = null;

      function esc(s) { const d = document.createElement('div'); d.textContent = s == null ? '' : s; return d.innerHTML; }
      function el(id) { return document.getElementById(id); }
      function setText(id, v) { const n = el(id); if (n) n.textContent = v; }

      function timeSince(dateStr) {
        const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
        if (seconds < 0 || isNaN(seconds)) return 'N/A';
        if (seconds < 60) return seconds + 's';
        const minutes = Math.floor(seconds / 60);
        if (minutes < 60) return minutes + 'm';
        const hours = Math.floor(minutes / 60);
        if (hours < 24) return hours + 'h ' + (minutes % 60) + 'm';
        const days = Math.floor(hours / 24);
        return days + 'd ' + (hours % 24) + 'h';
      }

      function statusHtml(running, text) {
        const color = running ? 'bg-emerald-500' : 'bg-red-400';
        const ring = running ? 'ring-emerald-500/20' : 'ring-red-400/20';
        const ping = running ? '<span class="absolute inline-flex h-full w-full animate-ping rounded-full ' + color + ' opacity-75"></span>' : '';
        const dot = '<span class="relative flex h-2.5 w-2.5">' + ping + '<span class="relative inline-flex h-2.5 w-2.5 rounded-full ' + color + ' ring-4 ' + ring + '"></span></span>';
        const textCls = running ? 'text-emerald-700' : 'text-red-600';
        return '<span class="flex items-center gap-2.5">' + dot + '<span class="' + textCls + ' font-medium">' + esc(text) + '</span></span>';
      }

      function tickUptime() {
        setText('tile-uptime', lastRunning && lastStartedAt ? timeSince(lastStartedAt) : 'N/A');
      }

      function updateTiles(s) {
        const st = el('tile-status'); if (st) st.innerHTML = statusHtml(s.status.running, s.status.status);
        setText('tile-cpu', s.stats.cpu + '%');
        setText('tile-mem', s.stats.memoryUsed + ' MB');
        setText('tile-mem-detail', s.stats.memoryPercent + '% of ' + s.stats.memoryLimit + ' MB');
        const si = s.serverInfo || {};
        setText('tile-hostname', si.hostname || 'N/A');
        setText('tile-players', (si.players || '0') + ' / ' + (si.maxPlayers || '0'));
        setText('tile-map', si.map || 'N/A');
        setText('tile-fps', si.fps || 'N/A');
        tickUptime();
      }

      function applyPending() {
        const banner = el('action-banner');
        el('action-banner-text').textContent = pending ? PENDING[pending.type].label : '';
        banner.classList.toggle('hidden', !pending);
        document.querySelectorAll('[data-action]').forEach(b => {
          b.disabled = !!pending;
          b.classList.toggle('opacity-50', !!pending);
          b.classList.toggle('pointer-events-none', !!pending);
        });
      }

      function updateGroups() {
        // While an action is in flight, hide both control sets and the live (running-only) controls.
        el('controls-running').classList.toggle('hidden', !!pending || !lastRunning);
        el('controls-stopped').classList.toggle('hidden', !!pending || lastRunning);
        const live = el('live-controls');
        if (live) live.classList.toggle('hidden', !!pending || !lastRunning);
      }

      function render(s) {
        lastRunning = !!s.status.running;
        lastStartedAt = s.status.startedAt || '';
        updateTiles(s);
        if (pending && (PENDING[pending.type].done(s, pending) || Date.now() > pending.deadline)) {
          pending = null;
        }
        applyPending();
        updateGroups();
        renderScheduler(s.scheduler);
      }

      function mmss(sec) {
        const m = Math.floor(sec / 60), x = sec % 60;
        return m + ':' + (x < 10 ? '0' : '') + x;
      }

      function tickAutoUpdate() {
        if (!autoRestartAt) return;
        const remain = Math.max(0, Math.round((autoRestartAt - Date.now()) / 1000));
        setText('autoupdate-text', 'Server update (build ' + (autoBuild || '?') + ') detected — auto-restart in ' + mmss(remain) + '. Players are being warned in-game.');
      }

      function renderScheduler(sch) {
        sch = sch || {};
        setText('autoupdate-status', sch.enabled
          ? 'Auto-update: on · checks every ' + sch.intervalMin + 'm · ' + sch.delayMin + 'm player warning before restart'
          : 'Auto-update: off');
        if (sch.restartScheduled && sch.restartAt) {
          autoRestartAt = sch.restartAt; autoBuild = sch.pendingBuild;
          el('autoupdate-banner').classList.remove('hidden');
          tickAutoUpdate();
        } else {
          autoRestartAt = null; autoBuild = null;
          el('autoupdate-banner').classList.add('hidden');
        }
      }

      function showError(msg) {
        const box = el('action-error');
        el('action-error-text').textContent = msg;
        box.classList.remove('hidden');
        clearTimeout(showError._t);
        showError._t = setTimeout(() => box.classList.add('hidden'), 6000);
      }

      async function refresh() {
        try {
          const res = await fetch('/api/server/status', { headers: { Accept: 'application/json' } });
          const data = await res.json();
          if (data && data.error === 'unauthorized') { location.href = '/login'; return; }
          if (data && data.status) render(data);
        } catch (e) { /* transient — keep last known state */ }
      }

      async function doAction(btn) {
        const action = btn.dataset.action;
        const confirmMsg = btn.dataset.confirm;
        if (confirmMsg && !confirm(confirmMsg)) return;
        el('update-result').classList.add('hidden');
        pending = { type: action, prevStartedAt: lastStartedAt, deadline: Date.now() + PENDING_TIMEOUT_MS };
        applyPending();
        updateGroups();
        try {
          const res = await fetch('/api/server/' + action, { method: 'POST' });
          const data = await res.json().catch(() => ({}));
          if (data && data.error === 'unauthorized') { location.href = '/login'; return; }
          if (data && data.error) { showError(data.error); pending = null; applyPending(); updateGroups(); return; }
        } catch (e) {
          showError('Request failed: ' + e.message); pending = null; applyPending(); updateGroups(); return;
        }
        setTimeout(refresh, 1500); // let Docker register the change, then resume polling
      }

      document.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (btn) { e.preventDefault(); doAction(btn); }
      });

      // ── Check for updates (build-id compare via SteamCMD) ──
      const checkBtn = el('check-updates-btn');
      const updateBox = el('update-result');
      const updateText = el('update-result-text');
      const doUpdateBtn = el('do-update-btn');
      const UPDATE_BASE = 'mb-4 flex items-center gap-3 rounded-lg border px-4 py-3 text-sm';

      function showUpdateResult(d) {
        doUpdateBtn.classList.add('hidden');
        let colors;
        if (d.error) {
          colors = 'border-red-200 bg-red-50 text-red-800';
          updateText.textContent = d.error;
        } else if (d.updateAvailable) {
          colors = 'border-amber-200 bg-amber-50 text-amber-800';
          updateText.textContent = 'Update available: build ' + (d.installed || 'none') + ' → ' + d.latest + ' (branch ' + d.branch + ').';
          doUpdateBtn.dataset.confirm = 'Update to build ' + d.latest + '? The server will restart and download the update on boot.';
          doUpdateBtn.classList.remove('hidden');
        } else {
          colors = 'border-emerald-200 bg-emerald-50 text-emerald-800';
          updateText.textContent = 'Up to date — build ' + (d.installed || d.latest || 'unknown') + ' (branch ' + d.branch + ').';
        }
        updateBox.className = UPDATE_BASE + ' ' + colors;
      }

      async function checkUpdates() {
        const orig = checkBtn.textContent;
        checkBtn.disabled = true; checkBtn.textContent = 'Checking…';
        try {
          const res = await fetch('/api/server/update/check');
          const data = await res.json();
          if (data && data.error === 'unauthorized') { location.href = '/login'; return; }
          showUpdateResult(data || { error: 'No response from server' });
        } catch (e) {
          showUpdateResult({ error: 'Check failed: ' + e.message });
        } finally {
          checkBtn.disabled = false; checkBtn.textContent = orig;
        }
      }

      if (checkBtn) checkBtn.addEventListener('click', checkUpdates);

      // ── Cancel a scheduled auto-update restart ──
      const cancelUpdateBtn = el('cancel-update-btn');
      if (cancelUpdateBtn) cancelUpdateBtn.addEventListener('click', async () => {
        cancelUpdateBtn.disabled = true;
        try {
          const res = await fetch('/api/server/update/cancel', { method: 'POST' });
          const d = await res.json().catch(() => ({}));
          if (d && d.error === 'unauthorized') { location.href = '/login'; return; }
        } catch (e) { /* ignore */ }
        finally { cancelUpdateBtn.disabled = false; }
        refresh();
      });

      refresh(); // sync scheduler banner + tiles immediately on load
      setInterval(refresh, POLL_MS);
      setInterval(() => { tickUptime(); tickAutoUpdate(); }, 1000);
    </script>
  `, { activePage: "dashboard" });
}

function timeSince(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 0) return "N/A";
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}m`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}
