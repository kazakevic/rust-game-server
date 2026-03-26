import { layout } from "./layout";
import { pageHeader, button } from "./components";

export function rconPage() {
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

    <div class="space-y-4">
      ${cmdGroup("Server Info", [
        ["status", "Status"],
        ["serverinfo", "Server Info"],
        ["playerlist", "Player List"],
        ["server.fps", "FPS"],
        ["global.perf 6", "Performance"],
      ])}
      ${cmdGroup("Server Control", [
        ["server.save", "Save World"],
        ["server.writecfg", "Write Config"],
        ['restart 300 "Server restarting in 5 minutes"', "Restart (5m)"],
        ["quit", "Stop Server"],
      ])}
      ${cmdGroup("World & Time", [
        ["env.time", "Current Time"],
        ["env.time 12", "Set Noon"],
        ["weather.fog 0", "Clear Fog"],
        ["weather.rain 0", "Clear Rain"],
        ["weather.clouds 0", "Clear Clouds"],
      ])}
      ${cmdGroup("Players", [
        ["users", "Connected"],
        ["sleepingusers", "Sleeping"],
        ["banlistex", "Ban List"],
        ['say "Hello everyone!"', "Broadcast"],
      ])}
      ${cmdGroup("Oxide / uMod", [
        ["oxide.reload *", "Reload All"],
        ["oxide.version", "Version"],
        ["plugins", "List Plugins"],
        ["oxide.unload *", "Unload All"],
        ["oxide.load *", "Load All"],
        ["oxide.grant group default vanish.allow", "Grant Perm"],
      ])}
      ${cmdGroup("Admin & Debug", [
        ["find .", "All Commands"],
        ["gc.collect", "Force GC"],
        ["pool.status", "Pool Status"],
        ["entity.stats", "Entity Stats"],
        ["server.seed", "Map Seed"],
        ["server.worldsize", "Map Size"],
      ])}
    </div>

    <script>
      const output = document.getElementById('output');
      const form = document.getElementById('rcon-form');
      const cmd = document.getElementById('cmd');
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

      document.querySelectorAll('[data-cmd]').forEach(btn => {
        btn.addEventListener('click', () => sendCommand(btn.dataset.cmd));
      });
    </script>
  `, { activePage: "rcon" });
}

function cmdGroup(title: string, commands: [string, string][]) {
  const buttons = commands
    .map(([cmd, label]) =>
      `<button data-cmd="${cmd}" class="inline-flex items-center rounded-lg border border-zinc-200 bg-white px-3 py-1.5 text-xs font-medium text-zinc-600 shadow-sm hover:bg-zinc-50 hover:text-zinc-900 transition-colors cursor-pointer" title="${cmd}">${label}</button>`
    )
    .join("\n        ");
  return `
    <div>
      <div class="text-xs font-medium text-zinc-400 uppercase tracking-wider mb-2">${title}</div>
      <div class="flex flex-wrap gap-1.5">
        ${buttons}
      </div>
    </div>`;
}
