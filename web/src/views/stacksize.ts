import { layout } from "./layout";
import { pageHeader, alert, section, input, button, escapeHtml } from "./components";

interface StackSizeData {
  config: Record<string, any> | null;
  error?: string;
  success?: string;
}

const CATEGORIES = [
  "Ammunition", "Attire", "Component", "Construction", "Electrical",
  "Food", "Fun", "Items", "Medical", "Misc", "Resources",
  "Tool", "Traps", "Weapon",
];

const QUICK_MULTIPLIERS = [1, 2, 5, 10, 20, 50];

export function stackSizePage(data: StackSizeData) {
  const { config, error, success } = data;

  const banner = error
    ? alert(error, "error")
    : success
    ? alert(success, "success")
    : "";

  if (!config) {
    return layout("Stack Sizes", `
      ${pageHeader("Stack Sizes", { description: "Configure StackSizeController plugin" })}
      ${banner || alert("Could not load config. Make sure StackSizeController is installed and the server has been started at least once.", "warning")}
    `, { activePage: "stacksize" });
  }

  const globalMultiplier = config["Default Stack Multiplier (applies to all items not specifically configured)"] ?? config["GlobalStackMultiplier"] ?? 1;
  const categoryMultipliers = config["Category Multipliers"] || {};
  const individualItems = config["Individual Item Multipliers"] || {};

  const itemEntries = Object.entries(individualItems)
    .sort(([a], [b]) => a.localeCompare(b));

  return layout("Stack Sizes", `
    ${pageHeader("Stack Sizes", { description: "Configure item stack size multipliers" })}
    ${banner ? `<div class="mb-6">${banner}</div>` : ""}

    <form method="POST" action="/api/config/stacksize/save" class="space-y-6">

      ${section("Global Multiplier", `
        <p class="text-sm text-zinc-500 mb-4">Applies to all items not configured individually or by category.</p>
        <div class="flex items-end gap-4 flex-wrap">
          ${input({ name: "global_multiplier", label: "Multiplier", type: "number", value: String(globalMultiplier), step: "0.1", class: "w-32" })}
          <div class="flex gap-2 pb-0.5">
            ${QUICK_MULTIPLIERS.map(m =>
              `<button type="button" onclick="document.querySelector('input[name=global_multiplier]').value='${m}'"
                class="h-9 px-3 text-xs font-medium rounded-lg transition-colors cursor-pointer ${m === globalMultiplier
                  ? 'bg-zinc-900 text-white'
                  : 'border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50'}">${m}x</button>`
            ).join("")}
          </div>
        </div>
      `)}

      ${section("Category Multipliers", `
        <p class="text-sm text-zinc-500 mb-4">Override stack sizes per item category. Set to 0 to use the global multiplier.</p>
        <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
          ${CATEGORIES.map(cat => `
            <div>
              <label class="block text-xs font-medium text-zinc-500 mb-1">${escapeHtml(cat)}</label>
              <input type="number" name="cat_${cat}" value="${categoryMultipliers[cat] ?? ""}" step="0.1" min="0"
                     placeholder="global"
                     class="flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent placeholder:text-zinc-300" />
            </div>
          `).join("")}
        </div>
        <div class="mt-3 flex gap-2">
          <button type="button" onclick="setAllCategories()"
                  class="inline-flex items-center justify-center rounded-lg h-8 px-4 text-xs font-medium border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 transition-colors cursor-pointer">
            Set all categories to global
          </button>
        </div>
      `, { description: "Per-category stack multipliers" })}

      ${section("Individual Item Overrides", `
        <p class="text-sm text-zinc-500 mb-4">Override specific items by shortname. These take priority over category and global multipliers.</p>
        <div class="mb-4">
          <input type="text" id="item-search" placeholder="Search items..."
                 class="flex h-9 w-full max-w-sm rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent placeholder:text-zinc-400" />
        </div>
        <div id="items-container">
          ${itemEntries.length > 0 ? `
            <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2" id="items-grid">
              ${itemEntries.map(([name, val]) => itemRow(name, val as number)).join("")}
            </div>
          ` : `
            <p class="text-sm text-zinc-400 italic">No individual overrides configured.</p>
          `}
        </div>
        <div class="mt-4 flex items-end gap-3">
          <div>
            <label class="block text-xs font-medium text-zinc-500 mb-1">Item shortname</label>
            <input type="text" id="new-item-name" placeholder="e.g. wood"
                   class="flex h-9 w-48 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent placeholder:text-zinc-400" />
          </div>
          <div>
            <label class="block text-xs font-medium text-zinc-500 mb-1">Multiplier</label>
            <input type="number" id="new-item-value" value="10" step="0.1" min="0"
                   class="flex h-9 w-24 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent" />
          </div>
          <button type="button" onclick="addItem()"
                  class="h-9 px-4 text-sm font-medium rounded-lg bg-zinc-900 text-white hover:bg-zinc-800 transition-colors cursor-pointer">Add</button>
        </div>
      `, { description: "Per-item stack multipliers" })}

      <div class="flex items-center gap-3">
        ${button("Save & Reload Plugin", { variant: "primary", size: "lg", type: "submit" })}
        <a href="/config/stacksize" class="inline-flex items-center justify-center rounded-lg h-10 px-6 text-sm font-medium border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 transition-colors">Reset Form</a>
      </div>
    </form>

    <script>
      (function() {
        // Search filter
        const searchInput = document.getElementById('item-search');
        if (searchInput) {
          searchInput.addEventListener('input', function() {
            const q = this.value.toLowerCase();
            document.querySelectorAll('#items-grid > div').forEach(function(row) {
              const name = row.getAttribute('data-item') || '';
              row.style.display = name.includes(q) ? '' : 'none';
            });
          });
        }

        // Set all categories to empty (use global)
        window.setAllCategories = function() {
          document.querySelectorAll('input[name^="cat_"]').forEach(function(inp) {
            inp.value = '';
          });
        };

        // Add new item override
        window.addItem = function() {
          const nameInput = document.getElementById('new-item-name');
          const valInput = document.getElementById('new-item-value');
          const name = nameInput.value.trim().toLowerCase();
          const val = valInput.value || '10';
          if (!name) return;

          // Check if already exists
          const existing = document.querySelector('input[name="item_' + CSS.escape(name) + '"]');
          if (existing) {
            existing.value = val;
            existing.closest('div[data-item]').classList.add('ring-2', 'ring-zinc-900');
            setTimeout(() => existing.closest('div[data-item]').classList.remove('ring-2', 'ring-zinc-900'), 1000);
            nameInput.value = '';
            return;
          }

          let grid = document.getElementById('items-grid');
          if (!grid) {
            const container = document.getElementById('items-container');
            container.innerHTML = '<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2" id="items-grid"></div>';
            grid = document.getElementById('items-grid');
          }

          const row = document.createElement('div');
          row.setAttribute('data-item', name);
          row.className = 'flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2';
          row.innerHTML = '<span class="text-sm font-mono text-zinc-700 flex-1 truncate">' + name + '</span>' +
            '<input type="number" name="item_' + name + '" value="' + val + '" step="0.1" min="0" ' +
            'class="h-7 w-20 rounded border border-zinc-300 bg-white px-2 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent text-right" />' +
            '<span class="text-xs text-zinc-400">x</span>' +
            '<button type="button" onclick="removeItem(this)" class="text-zinc-400 hover:text-red-500 transition-colors cursor-pointer ml-1">' +
            '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg></button>';
          grid.appendChild(row);
          nameInput.value = '';
        };

        // Remove item override
        window.removeItem = function(btn) {
          btn.closest('div[data-item]').remove();
        };
      })();
    </script>
  `, { activePage: "stacksize" });
}

function itemRow(name: string, value: number): string {
  return `<div data-item="${escapeHtml(name)}" class="flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2">
    <span class="text-sm font-mono text-zinc-700 flex-1 truncate">${escapeHtml(name)}</span>
    <input type="number" name="item_${escapeHtml(name)}" value="${value}" step="0.1" min="0"
           class="h-7 w-20 rounded border border-zinc-300 bg-white px-2 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent text-right" />
    <span class="text-xs text-zinc-400">x</span>
    <button type="button" onclick="removeItem(this)" class="text-zinc-400 hover:text-red-500 transition-colors cursor-pointer ml-1">
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
    </button>
  </div>`;
}
