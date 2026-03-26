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

    <div class="mt-4 flex flex-wrap gap-2">
      ${quickBtn("status")}
      ${quickBtn("serverinfo")}
      ${quickBtn("playerlist")}
      ${quickBtn("env.time")}
      ${quickBtn("server.save")}
      ${quickBtn("oxide.reload *")}
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

function quickBtn(cmd: string) {
  return `<button data-cmd="${cmd}" class="bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 text-xs px-3 py-1.5 rounded">${cmd}</button>`;
}
