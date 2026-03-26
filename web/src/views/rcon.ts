import { layout } from "./layout";

export function rconPage() {
  return layout("RCON Console", `
    <h2 class="text-2xl font-bold mb-6">RCON Console</h2>

    <div class="bg-gray-900 border border-gray-800 rounded-lg overflow-hidden">
      <div id="output" class="console-output bg-black p-4 h-96 overflow-y-auto text-green-400 whitespace-pre-wrap"></div>
      <form id="rcon-form" class="flex border-t border-gray-800">
        <span class="text-gray-500 px-3 py-3 text-sm">&gt;</span>
        <input id="cmd" type="text" autocomplete="off" placeholder="Enter RCON command..."
          class="flex-1 bg-transparent text-sm text-gray-200 py-3 focus:outline-none" />
        <button type="submit" class="bg-rust-600 hover:bg-rust-700 text-white text-sm px-6">Send</button>
      </form>
    </div>

    <div class="mt-4 space-y-3">
      ${cmdGroup("Server Info", [
        ["status", "Server status overview"],
        ["serverinfo", "Detailed server info"],
        ["playerlist", "List online players"],
        ["server.fps", "Current server FPS"],
        ["global.perf 6", "Performance report"],
      ])}
      ${cmdGroup("Server Control", [
        ["server.save", "Save world"],
        ["server.writecfg", "Save config to disk"],
        ["restart 300 \"Server restarting in 5 minutes\"", "Restart in 5 min"],
        ["quit", "Stop server"],
      ])}
      ${cmdGroup("World & Time", [
        ["env.time", "Current time"],
        ["env.time 12", "Set to noon"],
        ["weather.fog 0", "Clear fog"],
        ["weather.rain 0", "Clear rain"],
        ["weather.clouds 0", "Clear clouds"],
      ])}
      ${cmdGroup("Players", [
        ["users", "List connected users"],
        ["sleepingusers", "List sleeping players"],
        ["banlistex", "Show ban list"],
        ["say \"Hello everyone!\"", "Broadcast message"],
      ])}
      ${cmdGroup("Oxide / uMod", [
        ["oxide.reload *", "Reload all plugins"],
        ["oxide.version", "Oxide version"],
        ["plugins", "List loaded plugins"],
        ["oxide.unload *", "Unload all plugins"],
        ["oxide.load *", "Load all plugins"],
        ["oxide.grant group default vanish.allow", "Grant plugin perm"],
      ])}
      ${cmdGroup("Admin & Debug", [
        ["find .", "List all commands"],
        ["gc.collect", "Force garbage collection"],
        ["pool.status", "Memory pool status"],
        ["entity.stats", "Entity statistics"],
        ["server.seed", "Show map seed"],
        ["server.worldsize", "Show map size"],
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
          line.className = 'text-yellow-400 mt-2';
          line.textContent = '> ' + text;
        } else {
          line.className = 'text-green-400';
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
  `);
}

function cmdGroup(title: string, commands: [string, string][]) {
  const buttons = commands
    .map(([cmd, label]) =>
      `<button data-cmd="${cmd}" class="bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 text-xs px-3 py-1.5 rounded" title="${cmd}">${label}</button>`
    )
    .join("\n        ");
  return `
      <div>
        <div class="text-xs text-gray-500 uppercase tracking-wider mb-1.5">${title}</div>
        <div class="flex flex-wrap gap-2">
          ${buttons}
        </div>
      </div>`;
}
