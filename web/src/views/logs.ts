import { layout } from "./layout";

export function logsPage(logs: string) {
  const escapedLogs = logs
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");

  return layout("Server Logs", `
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-2xl font-bold">Server Logs</h2>
      <div class="flex items-center gap-3">
        <select id="tail-lines" class="bg-gray-800 border border-gray-700 text-gray-300 text-sm rounded px-3 py-1.5">
          <option value="100">Last 100 lines</option>
          <option value="200" selected>Last 200 lines</option>
          <option value="500">Last 500 lines</option>
          <option value="1000">Last 1000 lines</option>
        </select>
        <button id="refresh-btn" class="bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 text-sm px-4 py-1.5 rounded">Refresh</button>
        <label class="flex items-center gap-2 text-sm text-gray-400">
          <input type="checkbox" id="auto-scroll" checked class="accent-rust-500" /> Auto-scroll
        </label>
      </div>
    </div>

    <div class="bg-gray-900 border border-gray-800 rounded-lg overflow-hidden">
      <div id="log-output" class="console-output bg-black p-4 h-[70vh] overflow-y-auto text-gray-300 whitespace-pre-wrap">${escapedLogs}</div>
    </div>

    <script>
      const logOutput = document.getElementById('log-output');
      const tailSelect = document.getElementById('tail-lines');
      const refreshBtn = document.getElementById('refresh-btn');
      const autoScroll = document.getElementById('auto-scroll');

      function scrollToBottom() {
        if (autoScroll.checked) {
          logOutput.scrollTop = logOutput.scrollHeight;
        }
      }
      scrollToBottom();

      async function fetchLogs() {
        const tail = tailSelect.value;
        try {
          const res = await fetch('/api/logs?tail=' + tail);
          const data = await res.json();
          if (data.logs) {
            logOutput.textContent = data.logs;
            scrollToBottom();
          }
        } catch (e) {
          console.error('Failed to fetch logs:', e);
        }
      }

      refreshBtn.addEventListener('click', fetchLogs);
      tailSelect.addEventListener('change', fetchLogs);

      // Auto-refresh every 5 seconds
      setInterval(fetchLogs, 5000);
    </script>
  `);
}
