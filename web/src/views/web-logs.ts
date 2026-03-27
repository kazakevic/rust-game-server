import { layout } from "./layout";
import { pageHeader, button, select, badge, escapeHtml } from "./components";
import type { LogEntry } from "../logger";

const levelColors: Record<string, string> = {
  info: "text-blue-400",
  warn: "text-amber-400",
  error: "text-red-400",
};

const levelBadge: Record<string, string> = {
  info: "bg-blue-500/15 text-blue-400",
  warn: "bg-amber-500/15 text-amber-400",
  error: "bg-red-500/15 text-red-400",
};

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function renderLogLine(entry: LogEntry): string {
  const time = escapeHtml(formatTimestamp(entry.timestamp));
  const lvl = entry.level.toUpperCase().padEnd(5);
  const cat = escapeHtml(entry.category);
  const msg = escapeHtml(entry.message);
  const color = levelColors[entry.level] || "text-zinc-400";
  return `<div class="flex gap-3 py-0.5 hover:bg-white/5 px-1 rounded"><span class="text-zinc-600 shrink-0">${time}</span><span class="shrink-0 w-12 font-semibold ${color}">${lvl}</span><span class="text-zinc-500 shrink-0">[${cat}]</span><span class="text-zinc-300">${msg}</span></div>`;
}

export function webLogsPage(logs: LogEntry[], categories: string[]) {
  const tailSelect = select({
    id: "tail-lines",
    items: [
      { value: "100", label: "Last 100 entries" },
      { value: "200", label: "Last 200 entries", selected: true },
      { value: "500", label: "Last 500 entries" },
      { value: "1000", label: "Last 1000 entries" },
    ],
  });

  const levelSelect = select({
    id: "level-filter",
    items: [
      { value: "", label: "All levels" },
      { value: "info", label: "Info" },
      { value: "warn", label: "Warning" },
      { value: "error", label: "Error" },
    ],
  });

  const categoryItems = [{ value: "", label: "All categories" }, ...categories.map(c => ({ value: c, label: c }))];
  const categorySelect = select({ id: "category-filter", items: categoryItems });

  const logLines = logs.map(renderLogLine).join("");

  return layout("Web Logs", `
    ${pageHeader("Web Logs", {
      description: "Admin dashboard activity log",
      actions: `
        ${levelSelect}
        ${categorySelect}
        ${tailSelect}
        ${button("Refresh", { variant: "outline", size: "sm", attrs: 'id="refresh-btn"' })}
        <label class="flex items-center gap-2 text-sm text-zinc-500 ml-2 cursor-pointer select-none">
          <input type="checkbox" id="auto-scroll" checked class="h-3.5 w-3.5 rounded border-zinc-300 text-zinc-900 focus:ring-zinc-950 cursor-pointer" />
          Auto-scroll
        </label>
      `,
    })}

    <div class="rounded-xl border border-zinc-200 bg-white shadow-sm overflow-hidden">
      <div id="log-output" class="console-output bg-zinc-950 text-zinc-400 p-4 h-[70vh] overflow-y-auto">${logLines || '<div class="text-zinc-600 text-center py-8">No log entries yet</div>'}</div>
    </div>

    <script>
      const logOutput = document.getElementById('log-output');
      const tailSelect = document.getElementById('tail-lines');
      const levelFilter = document.getElementById('level-filter');
      const categoryFilter = document.getElementById('category-filter');
      const refreshBtn = document.getElementById('refresh-btn');
      const autoScroll = document.getElementById('auto-scroll');

      function scrollToBottom() {
        if (autoScroll.checked) {
          logOutput.scrollTop = logOutput.scrollHeight;
        }
      }
      scrollToBottom();

      const levelColors = { info: 'text-blue-400', warn: 'text-amber-400', error: 'text-red-400' };

      function esc(s) {
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
      }

      function fmtTime(iso) {
        const d = new Date(iso);
        return d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      }

      function renderEntry(e) {
        const color = levelColors[e.level] || 'text-zinc-400';
        return '<div class="flex gap-3 py-0.5 hover:bg-white/5 px-1 rounded">'
          + '<span class="text-zinc-600 shrink-0">' + esc(fmtTime(e.timestamp)) + '</span>'
          + '<span class="shrink-0 w-12 font-semibold ' + color + '">' + e.level.toUpperCase().padEnd(5) + '</span>'
          + '<span class="text-zinc-500 shrink-0">[' + esc(e.category) + ']</span>'
          + '<span class="text-zinc-300">' + esc(e.message) + '</span>'
          + '</div>';
      }

      async function fetchLogs() {
        const params = new URLSearchParams();
        params.set('tail', tailSelect.value);
        if (levelFilter.value) params.set('level', levelFilter.value);
        if (categoryFilter.value) params.set('category', categoryFilter.value);
        try {
          const res = await fetch('/api/web-logs?' + params);
          const data = await res.json();
          if (data.logs) {
            logOutput.innerHTML = data.logs.length
              ? data.logs.map(renderEntry).join('')
              : '<div class="text-zinc-600 text-center py-8">No log entries match filters</div>';
            scrollToBottom();
          }
        } catch (e) {
          console.error('Failed to fetch web logs:', e);
        }
      }

      refreshBtn.addEventListener('click', fetchLogs);
      tailSelect.addEventListener('change', fetchLogs);
      levelFilter.addEventListener('change', fetchLogs);
      categoryFilter.addEventListener('change', fetchLogs);
      setInterval(fetchLogs, 5000);
    </script>
  `, { activePage: "web-logs" });
}
