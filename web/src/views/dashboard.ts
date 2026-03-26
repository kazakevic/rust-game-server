import { layout } from "./layout";
import { statsCard, statusDot, button, pageHeader, section } from "./components";

interface DashboardData {
  status: { running: boolean; status: string; startedAt: string };
  stats: { cpu: string; memoryUsed: string; memoryLimit: string; memoryPercent: string };
  serverInfo: { hostname: string; players: string; maxPlayers: string; map: string; fps: string };
}

export function dashboardPage(data: DashboardData) {
  const { status, stats, serverInfo } = data;
  const uptime = status.startedAt ? timeSince(status.startedAt) : "N/A";

  const serverControls = status.running
    ? `<form method="POST" action="/api/server/restart" onsubmit="return confirm('Are you sure you want to restart the server?')">
        ${button("Restart", { variant: "warning", size: "sm", type: "submit" })}
      </form>
      <form method="POST" action="/api/server/stop" onsubmit="return confirm('Are you sure you want to stop the server?')">
        ${button("Stop", { variant: "destructive", size: "sm", type: "submit" })}
      </form>`
    : `<form method="POST" action="/api/server/start">
        ${button("Start Server", { variant: "success", size: "sm", type: "submit" })}
      </form>`;

  const statusValue = `<span class="flex items-center gap-2.5">
    ${statusDot(status.running)}
    <span class="${status.running ? "text-emerald-700" : "text-red-600"} font-medium">${status.status}</span>
  </span>`;

  return layout("Dashboard", `
    ${pageHeader("Dashboard", { actions: `<div class="flex items-center gap-2">${serverControls}</div>` })}

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
      ${statsCard("Status", "", { icon: statusValue })}
      ${statsCard("Uptime", uptime)}
      ${statsCard("CPU", `${stats.cpu}%`)}
      ${statsCard("Memory", `${stats.memoryUsed} MB`, { detail: `${stats.memoryPercent}% of ${stats.memoryLimit} MB` })}
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      ${statsCard("Hostname", serverInfo.hostname || "N/A")}
      ${statsCard("Players", `${serverInfo.players || "0"} / ${serverInfo.maxPlayers || "0"}`)}
      ${statsCard("Map", serverInfo.map || "N/A")}
      ${statsCard("Server FPS", serverInfo.fps || "N/A")}
    </div>

    ${status.running ? `
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
      ${section("Plugin Controls", `
        <div class="flex flex-wrap gap-2">
          <form method="POST" action="/api/plugins/reload-all">
            ${button("Reload All Plugins", { variant: "outline", size: "sm", type: "submit" })}
          </form>
          <form method="POST" action="/api/plugins/reload-gungame">
            ${button("Reload GunGame", { variant: "outline", size: "sm", type: "submit" })}
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
    ` : ""}

    <script>setTimeout(() => location.reload(), 15000);</script>
  `, { activePage: "dashboard" });
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
