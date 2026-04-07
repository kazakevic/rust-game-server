import { layout } from "./layout";
import { pageHeader, card, button, alert, badge, emptyState, escapeHtml } from "./components";

export function pluginsPage(data: {
  plugins: string[];
  error?: string;
  success?: string;
}) {
  const { plugins } = data;

  const content = `
    ${pageHeader("uMod Plugins", {
      description: "Manage plugins downloaded from umod.org on next server start or reinstall.",
      actions: `
        <form method="POST" action="/api/plugins/umod/reinstall" onsubmit="return confirm('This will re-run the plugin installer. Continue?')">
          ${button(`<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582M20 20v-5h-.582M4.582 9A8 8 0 0120 15M19.418 15A8 8 0 014 9"/></svg> Reinstall Plugins`, { variant: "secondary", type: "submit" })}
        </form>
      `,
    })}

    ${data.error ? `<div class="mb-4">${alert(data.error, "error")}</div>` : ""}
    ${data.success ? `<div class="mb-4">${alert(data.success, "success")}</div>` : ""}

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

      <!-- Plugin list -->
      <div class="lg:col-span-2">
        ${card({ title: "Installed Plugins", description: `${plugins.length} plugin${plugins.length !== 1 ? "s" : ""} configured` }, plugins.length === 0
          ? emptyState("No plugins configured yet. Add a plugin name on the right.")
          : `<div id="plugin-list" class="divide-y divide-zinc-100">
              ${plugins.map(p => `
                <div class="flex items-center justify-between py-2.5 gap-3" id="row-${escapeHtml(p)}">
                  <div class="flex items-center gap-2.5 min-w-0">
                    <svg class="w-4 h-4 text-zinc-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h7"/></svg>
                    <span class="text-sm font-medium text-zinc-800 truncate">${escapeHtml(p)}</span>
                  </div>
                  <div class="flex items-center gap-2 shrink-0">
                    <a href="https://umod.org/plugins/${escapeHtml(p)}" target="_blank" rel="noopener"
                       class="text-xs text-zinc-400 hover:text-zinc-700 transition-colors" title="View on umod.org">
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
                    </a>
                    <button type="button" onclick="removePlugin('${escapeHtml(p)}')"
                            class="text-zinc-400 hover:text-red-500 transition-colors p-0.5 rounded" title="Remove">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
                    </button>
                  </div>
                </div>
              `).join("")}
            </div>`
        )}
      </div>

      <!-- Add plugin + save -->
      <div class="space-y-4">
        ${card({ title: "Add Plugin" }, `
          <div class="space-y-3">
            <div>
              <label class="block text-sm font-medium text-zinc-700 mb-1.5">Plugin Name</label>
              <input type="text" id="new-plugin-input" placeholder="e.g. Vanish"
                     class="flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm transition-colors placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent" />
              <p class="mt-1 text-xs text-zinc-400">Must match the plugin name on umod.org exactly.</p>
            </div>
            ${button("Add Plugin", { variant: "outline", attrs: 'id="add-btn" onclick="addPlugin()"', class: "w-full" })}
          </div>
        `)}

        ${card({ title: "Save Changes" }, `
          <p class="text-sm text-zinc-500 mb-3">Changes are applied when you save. Use <strong>Reinstall Plugins</strong> to download any newly added plugins immediately.</p>
          <form method="POST" action="/api/plugins/umod/save" id="save-form">
            <input type="hidden" name="plugins" id="plugins-data" value="${escapeHtml(plugins.join("\n"))}" />
            ${button("Save Plugin List", { variant: "primary", type: "submit", class: "w-full" })}
          </form>
        `)}
      </div>
    </div>

    <script>
      let pluginList = ${JSON.stringify(plugins)};

      function renderList() {
        const container = document.getElementById('plugin-list');
        if (!container) return;

        if (pluginList.length === 0) {
          const parent = container.parentElement;
          parent.innerHTML = \`<div class="flex flex-col items-center justify-center py-12 text-center">
            <svg class="w-10 h-10 text-zinc-300" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 6h16M4 12h16M4 18h7"/></svg>
            <p class="mt-3 text-sm text-zinc-500">No plugins configured yet. Add a plugin name on the right.</p>
          </div>\`;
          return;
        }

        container.innerHTML = pluginList.map(p => \`
          <div class="flex items-center justify-between py-2.5 gap-3">
            <div class="flex items-center gap-2.5 min-w-0">
              <svg class="w-4 h-4 text-zinc-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h7"/></svg>
              <span class="text-sm font-medium text-zinc-800 truncate">\${p}</span>
            </div>
            <div class="flex items-center gap-2 shrink-0">
              <a href="https://umod.org/plugins/\${p}" target="_blank" rel="noopener"
                 class="text-xs text-zinc-400 hover:text-zinc-700 transition-colors" title="View on umod.org">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
              </a>
              <button type="button" onclick="removePlugin('\${p}')"
                      class="text-zinc-400 hover:text-red-500 transition-colors p-0.5 rounded" title="Remove">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
              </button>
            </div>
          </div>
        \`).join('');

        syncCount();
      }

      function syncCount() {
        const desc = document.querySelector('[data-plugin-count]');
        if (desc) desc.textContent = pluginList.length + ' plugin' + (pluginList.length !== 1 ? 's' : '') + ' configured';
      }

      function syncData() {
        document.getElementById('plugins-data').value = pluginList.join('\\n');
      }

      function addPlugin() {
        const input = document.getElementById('new-plugin-input');
        const name = input.value.trim();
        if (!name) return;
        if (pluginList.includes(name)) {
          input.value = '';
          return;
        }
        pluginList.push(name);
        input.value = '';
        renderList();
        syncData();
      }

      function removePlugin(name) {
        pluginList = pluginList.filter(p => p !== name);
        renderList();
        syncData();
      }

      document.getElementById('new-plugin-input').addEventListener('keydown', function(e) {
        if (e.key === 'Enter') { e.preventDefault(); addPlugin(); }
      });
    </script>
  `;

  return layout("uMod Plugins", content, { activePage: "plugins" });
}
