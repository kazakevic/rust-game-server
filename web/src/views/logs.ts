import { layout } from "./layout";
import { pageHeader, button, select, escapeHtml } from "./components";

export function logsPage(logs: string) {
  const escapedLogs = escapeHtml(logs);

  const tailSelect = select({
    id: "tail-lines",
    items: [
      { value: "100", label: "Last 100 lines" },
      { value: "200", label: "Last 200 lines", selected: true },
      { value: "500", label: "Last 500 lines" },
      { value: "1000", label: "Last 1000 lines" },
    ],
  });

  return layout("Server Logs", `
    ${pageHeader("Server Logs", {
      description: "Real-time Rust server output",
      actions: `
        ${tailSelect}
        ${button("Refresh", { variant: "outline", size: "sm", attrs: 'id="refresh-btn"' })}
        <label class="flex items-center gap-2 text-sm text-zinc-500 ml-2 cursor-pointer select-none">
          <input type="checkbox" id="auto-scroll" checked class="h-3.5 w-3.5 rounded border-zinc-300 text-zinc-900 focus:ring-zinc-950 cursor-pointer" />
          Auto-scroll
        </label>
      `,
    })}

    <div class="rounded-xl border border-zinc-200 bg-white shadow-sm overflow-hidden">
      <div id="log-output" class="console-output bg-zinc-950 text-zinc-400 p-4 h-[70vh] overflow-y-auto whitespace-pre-wrap">${escapedLogs}</div>
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
          const res = await fetch('/api/server-logs?tail=' + tail);
          const data = await res.json();
          if (data.logs) {
            const distFromBottom = logOutput.scrollHeight - logOutput.scrollTop - logOutput.clientHeight;
            logOutput.textContent = data.logs;
            if (autoScroll.checked) {
              logOutput.scrollTop = logOutput.scrollHeight;
            } else {
              logOutput.scrollTop = logOutput.scrollHeight - logOutput.clientHeight - distFromBottom;
            }
          }
        } catch (e) {
          console.error('Failed to fetch logs:', e);
        }
      }

      refreshBtn.addEventListener('click', fetchLogs);
      tailSelect.addEventListener('change', fetchLogs);
      setInterval(fetchLogs, 5000);
    </script>
  `, { activePage: "server-logs" });
}
