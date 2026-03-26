import { layout } from "./layout";

interface DashboardData {
  status: { running: boolean; status: string; startedAt: string };
  stats: { cpu: string; memoryUsed: string; memoryLimit: string; memoryPercent: string };
  serverInfo: { hostname: string; players: string; maxPlayers: string; map: string; fps: string };
}

export function dashboardPage(data: DashboardData) {
  const { status, stats, serverInfo } = data;
  const uptime = status.startedAt ? timeSince(status.startedAt) : "N/A";
  const statusColor = status.running ? "text-green-400" : "text-red-400";
  const statusDot = status.running ? "bg-green-400" : "bg-red-400";

  return layout("Dashboard", `
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-2xl font-bold">Server Dashboard</h2>
      <div class="flex gap-2">
        ${status.running ? `
          <form method="POST" action="/api/server/restart">
            <button class="bg-yellow-600 hover:bg-yellow-700 text-white text-sm px-4 py-2 rounded">Restart</button>
          </form>
          <form method="POST" action="/api/server/stop">
            <button class="bg-red-600 hover:bg-red-700 text-white text-sm px-4 py-2 rounded">Stop</button>
          </form>
        ` : `
          <form method="POST" action="/api/server/start">
            <button class="bg-green-600 hover:bg-green-700 text-white text-sm px-4 py-2 rounded">Start</button>
          </form>
        `}
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
      ${card("Status", `<span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full ${statusDot}"></span><span class="${statusColor} font-medium">${status.status}</span></span>`)}
      ${card("Uptime", uptime)}
      ${card("CPU", `${stats.cpu}%`)}
      ${card("Memory", `${stats.memoryUsed} MB / ${stats.memoryLimit} MB (${stats.memoryPercent}%)`)}
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
      ${card("Hostname", serverInfo.hostname || "N/A")}
      ${card("Players", `${serverInfo.players || "0"} / ${serverInfo.maxPlayers || "0"}`)}
      ${card("Map", serverInfo.map || "N/A")}
      ${card("FPS", serverInfo.fps || "N/A")}
    </div>

    ${status.running ? `
    <div class="bg-gray-900 border border-gray-800 rounded-lg p-6">
      <h3 class="text-lg font-semibold mb-4">Plugin Controls</h3>
      <div class="flex flex-wrap gap-3">
        <form method="POST" action="/api/plugins/reload-all">
          <button class="bg-blue-600 hover:bg-blue-700 text-white text-sm px-4 py-2 rounded">Reload All Plugins</button>
        </form>
        <form method="POST" action="/api/plugins/reload-gungame">
          <button class="bg-purple-600 hover:bg-purple-700 text-white text-sm px-4 py-2 rounded">Reload GunGame</button>
        </form>
        <form method="POST" action="/api/plugins/redownload">
          <button class="bg-orange-600 hover:bg-orange-700 text-white text-sm px-4 py-2 rounded"
                  onclick="this.disabled=true;this.textContent='Downloading...';">Re-download Plugins</button>
        </form>
      </div>
      <h3 class="text-lg font-semibold mt-6 mb-4">World Controls</h3>
      <div class="flex flex-wrap gap-3">
        <form method="POST" action="/api/world/set-day">
          <button class="bg-amber-500 hover:bg-amber-600 text-white text-sm px-4 py-2 rounded">Set Day</button>
        </form>
        <form method="POST" action="/api/world/set-night">
          <button class="bg-indigo-600 hover:bg-indigo-700 text-white text-sm px-4 py-2 rounded">Set Night</button>
        </form>
      </div>
      <h3 class="text-lg font-semibold mt-6 mb-4">Weather Controls</h3>
      <div class="flex flex-wrap gap-3">
        <form method="POST" action="/api/weather/clear">
          <button class="bg-sky-500 hover:bg-sky-600 text-white text-sm px-4 py-2 rounded">Clear Weather</button>
        </form>
        <form method="POST" action="/api/weather/rain">
          <button class="bg-blue-700 hover:bg-blue-800 text-white text-sm px-4 py-2 rounded">Rain</button>
        </form>
        <form method="POST" action="/api/weather/fog">
          <button class="bg-gray-500 hover:bg-gray-600 text-white text-sm px-4 py-2 rounded">Fog</button>
        </form>
        <form method="POST" action="/api/weather/storm">
          <button class="bg-gray-700 hover:bg-gray-800 text-white text-sm px-4 py-2 rounded">Storm</button>
        </form>
      </div>
    </div>
    ` : ""}

    <script>setTimeout(() => location.reload(), 15000);</script>
  `);
}

function card(title: string, value: string) {
  return `
    <div class="bg-gray-900 border border-gray-800 rounded-lg p-4">
      <div class="text-xs text-gray-500 uppercase tracking-wider mb-1">${title}</div>
      <div class="text-lg">${value}</div>
    </div>`;
}

function timeSince(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}m`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}
